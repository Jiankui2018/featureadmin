﻿using Caliburn.Micro;
using FeatureAdmin.Core.Contracts.Repositories;
using FeatureAdmin.ViewModels;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace FeatureAdmin.Tests
{
    public class AppViewModelTests
    {
        // The interfaces/instances you will need to test with - this is your test subject
        AppViewModel _mainVM;

        // You can mock the other interfaces:
        Mock<IWindowManager> _windowManager;
        Mock<IEventAggregator> _eventAggregator;
        Mock<ISharePointRepositoryRead> _readRepository;
         Mock<ISharePointRepositoryCommand> _commandRepository;

        public AppViewModelTests()
        {
            // Mock the window manager
            _windowManager = new Mock<IWindowManager>();

            // Mock the event aggregator
            _eventAggregator = new Mock<IEventAggregator>();

            _readRepository = new Mock<ISharePointRepositoryRead>();
            _commandRepository = new Mock<ISharePointRepositoryCommand>();

            // Create the main VM injecting the mocked interfaces
            // Mocking interfaces is always good as there is a lot of freedom
            // Use mock.Object to get hold of the object, the mock is just a proxy that decorates the original object
            _mainVM = new AppViewModel( _eventAggregator.Object, _readRepository.Object,
                        _commandRepository.Object);
        }

        [Fact]
        public void Test_SubscribedToEventAggregator()
        {
            // Test to make sure subscribe was called on the event aggregator at least once
            _eventAggregator.Verify(x => x.Subscribe(_mainVM));
        }
    }
}
