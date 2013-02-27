﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using ReallySimpleEventing.ActivationStrategies;
using ReallySimpleEventing.EventHandling;
using ReallySimpleEventing.ThreadingStrategies;

namespace ReallySimpleEventing.Test.Unit
{
    [TestFixture]
    public class EventsTests
    {
        private EventStream _eventStream;

        [SetUp]
        public void SetUp()
        {
            _eventStream = (EventStream)ReallySimpleEventing.CreateEventStream();
        }

        public class EventHandledBySingle {}
        public class EventHandledBySingleHandler : TestHandler<EventHandledBySingle> { }

        [Test]
        public void Raise_HandlerExist_HandlerExecuted()
        {
            var executionCount = 0;
            EventHandledBySingleHandler.OnHandleAction = () => executionCount++;

            _eventStream.Raise(new EventHandledBySingle());

            Assert.That(executionCount, Is.EqualTo(1));
        }

        [Test]
        public void Raise_RaiseOneMillionTrivialEvents_HandlerExecutedInLessThanTenSeconds()
        {
            const int numberOfEventsToRaise = 1000000;
            const int maxTimeAllowableInSeconds = 10;
            var executionCount = 0;
            EventHandledBySingleHandler.OnHandleAction = () => executionCount++;

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            for (var i = 0; i < numberOfEventsToRaise; i++)
            {
                _eventStream.Raise(new EventHandledBySingle());
            }

            stopWatch.Stop();

            Assert.That(executionCount, Is.EqualTo(numberOfEventsToRaise));
            Assert.That(stopWatch.Elapsed.Seconds, Is.LessThan(maxTimeAllowableInSeconds));
        }

        public class EventHandledByMultiple { }
        public class EventHandledByMultipleHandler1 : TestHandler<EventHandledByMultiple> { }
        public class EventHandledByMultipleHandler2 : TestHandler<EventHandledByMultiple> { }

        [Test]
        public void Raise_MultipleHandlersExist_AllHandlersExecuted()
        {
            var executionCount = 0;
            TestHandler<EventHandledByMultiple>.OnHandleAction = () => executionCount++;

            _eventStream.Raise(new EventHandledByMultiple());

            Assert.That(executionCount, Is.EqualTo(2));
            Assert.That(executionCount, Is.EqualTo(2));
        }

        public class EventWhereHandlerIsAsync { }
        public class AsycHandlerForEventWhereHandlerIsAsync : TestHandlerAsync<EventWhereHandlerIsAsync> { }

        [Test]
        public void Raise_AsyncHandlerExists_HandlerExecuted()
        {
            var called = false;
            AsycHandlerForEventWhereHandlerIsAsync.OnHandleAction = () => called = true;

            _eventStream.Raise(new EventWhereHandlerIsAsync());

            Thread.Sleep(100); // Hack to let async callback happen
            Assert.That(called, Is.True);
        }

        public class EventWhereHandlerThrowsOnError { }
        public class DelegatedHandler : TestHandler<EventWhereHandlerThrowsOnError> { }

        [Test]
        public void Raise_ErrorIsThrownInHandling_NoExceptionsCascade()
        {
            var initialException = new Exception("initial exception");
            DelegatedHandler.OnHandleAction = () => { throw initialException; };

            _eventStream.Raise(new EventWhereHandlerThrowsOnError());
        }

        [Test]
        public void Raise_HandlerExplicitlyThrowsOnError_HandlerCascadesOriginalException()
        {
            var initialException = new Exception("initial exception");
            DelegatedHandler.OnHandleAction = () => { throw initialException; };
            DelegatedHandler.OnErrorAction = e => { throw e; };

            var ex = Assert.Throws<Exception>(() => _eventStream.Raise(new EventWhereHandlerThrowsOnError()));

            Assert.That(ex.Message, Is.EqualTo(initialException.Message));
        }

        [Test]
        public void Raise_WhenEventFired_StrategyWhichSupportsEventHandlerSelected()
        {
            var strategyToSkip = new TestThreadStrategy(false);
            var strategyToSelect = new TestThreadStrategy(true);
            var threadStrategies = new List<IHandlerThreadingStrategy> {strategyToSkip, strategyToSelect};
            var stream = new EventStream(new EventHandlerResolver(), new ActivatorActivation(), threadStrategies);

            stream.Raise(new EventHandledBySingle());

            Assert.That(strategyToSelect.WasExecuted);
        }

        [Test]
        public void Raise_MultipleExecutionStrategiesMatch_FirstStrategyWhichSupportsEventHandlerSelected()
        {
            var firstMatch = new TestThreadStrategy(true);
            var secondMatch = new TestThreadStrategy(true);
            var threadStrategies = new List<IHandlerThreadingStrategy> {firstMatch, secondMatch};
            var stream = new EventStream(new EventHandlerResolver(), new ActivatorActivation(), threadStrategies);

            stream.Raise(new EventHandledBySingle());

            Assert.That(firstMatch.WasExecuted);
        }
    }

    public class TestThreadStrategy : IHandlerThreadingStrategy
    {
        private readonly bool _supports;
        public bool WasExecuted { get; private set; }

        public TestThreadStrategy(bool supports)
        {
            _supports = supports;
        }

        public bool Supports<TEventType>(IHandle<TEventType> handler)
        {
            return _supports;
        }

        public void Run(Action operation)
        {
            WasExecuted = true;
        }
    }
}
