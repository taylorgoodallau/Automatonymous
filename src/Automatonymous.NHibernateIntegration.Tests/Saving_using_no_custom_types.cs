namespace Automatonymous.NHibernateIntegration.Tests
{
    using System;
    using System.Threading.Tasks;
    using NHibernate;
    using NHibernate.Mapping.ByCode;
    using NHibernate.Mapping.ByCode.Conformist;
    using NUnit.Framework;
    using UserTypes;


    [TestFixture]
    public class Saving_using_no_custom_types
    {
        [Test]
        public async void Should_have_the_state_machine()
        {
            Guid correlationId = Guid.NewGuid();

            await RaiseEvent(correlationId, _machine.ExitFrontDoor, new GirlfriendYelling
            {
                CorrelationId = correlationId
            });

            await RaiseEvent(correlationId, _machine.GotHitByCar, new GotHitByACar
            {
                CorrelationId = correlationId
            });

            ShoppingChore instance = await GetStateMachine(correlationId);

            Assert.IsTrue(instance.Screwed);
        }

        SuperShopper _machine;
        ISessionFactory _sessionFactory;

        [TestFixtureSetUp]
        public void Setup()
        {
            _machine = new SuperShopper();
            AutomatonymousStateUserType<SuperShopper>.SaveAsString(_machine);

            _sessionFactory = new SQLiteSessionFactoryProvider(typeof(ShoppingChoreMap))
                .GetSessionFactory();
        }

        [TestFixtureTearDown]
        public void Teardown()
        {
            _sessionFactory.Dispose();
        }

        async Task RaiseEvent<T>(Guid id, Event<T> @event, T data)
        {
            using (ISession session = _sessionFactory.OpenSession())
            using (ITransaction transaction = session.BeginTransaction())
            {
                bool save = false;
                var instance = session.Get<ShoppingChore>(id, LockMode.Upgrade);
                if (instance == null)
                {
                    instance = new ShoppingChore(id);
                    save = true;
                }

                await _machine.RaiseEvent(instance, @event, data);

                if (save)
                    session.Save(instance);
                else
                    session.Update(instance);

                transaction.Commit();
            }
        }

        async Task<ShoppingChore> GetStateMachine(Guid id)
        {
            using (ISession session = _sessionFactory.OpenSession())
            using (ITransaction transaction = session.BeginTransaction())
            {
                var result = session.QueryOver<ShoppingChore>()
                    .Where(x => x.CorrelationId == id)
                    .SingleOrDefault<ShoppingChore>();

                transaction.Commit();

                return result;
            }
        }


        class ShoppingChoreMap :
            ClassMapping<ShoppingChore>
        {
            public ShoppingChoreMap()
            {
                Lazy(false);
                Table("ShoppingChore");

                Id(x => x.CorrelationId, x => x.Generator(Generators.Assigned));

                Property(x => x.CurrentState);
                Property(x => x.Everything);

                Property(x => x.Screwed);
            }
        }


        /// <summary>
        ///     Why to exit the door to go shopping
        /// </summary>
        class GirlfriendYelling
        {
            public Guid CorrelationId { get; set; }
        }


        class GotHitByACar
        {
            public Guid CorrelationId { get; set; }
        }


        class ShoppingChore
        {
            protected ShoppingChore()
            {
            }

            public ShoppingChore(Guid correlationId)
            {
                CorrelationId = correlationId;
            }

            public Guid CorrelationId { get; set; }
            public string CurrentState { get; set; }
            public int Everything { get; set; }
            public bool Screwed { get; set; }
        }


        class SuperShopper :
            AutomatonymousStateMachine<ShoppingChore>
        {
            public SuperShopper()
            {
                InstanceState(x => x.CurrentState);

                CompositeEvent(() => EndOfTheWorld, x => x.Everything, CompositeEventOptions.IncludeInitial, ExitFrontDoor, GotHitByCar);

                Initially(
                    When(ExitFrontDoor)
                        .Then(context => Console.Write("Leaving!"))
                        .TransitionTo(OnTheWayToTheStore));

                During(OnTheWayToTheStore,
                    When(GotHitByCar)
                        .Then(context => Console.WriteLine("Ouch!!"))
                        .Finalize());

                DuringAny(
                    When(EndOfTheWorld)
                        .Then(context => Console.WriteLine("Screwed!!"))
                        .Then(context => context.Instance.Screwed = true));
            }

            public Event<GirlfriendYelling> ExitFrontDoor { get; private set; }
            public Event<GotHitByACar> GotHitByCar { get; private set; }
            public Event EndOfTheWorld { get; private set; }

            public State OnTheWayToTheStore { get; private set; }
        }
    }
}