﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

/// <summary>
/// The test method info tests.
/// </summary>
public class TestMethodInfoTests : TestContainer
{
    private readonly TestMethodInfo _testMethodInfo;

    private readonly MethodInfo _methodInfo;

    private readonly TestClassAttribute _classAttribute;

    private readonly TestMethodAttribute _testMethodAttribute;

    private readonly TestAssemblyInfo _testAssemblyInfo;

    private readonly ConstructorInfo _constructorInfo;

    private readonly TestContextImplementation _testContextImplementation;

    private readonly TestClassInfo _testClassInfo;

    private readonly ExpectedExceptionAttribute _expectedException;

    public TestMethodInfoTests()
    {
        _constructorInfo = typeof(DummyTestClass).GetConstructor([])!;
        _methodInfo = typeof(DummyTestClass).GetMethods().Single(m => m.Name.Equals("DummyTestMethod", StringComparison.Ordinal));
        _classAttribute = new TestClassAttribute();
        _testMethodAttribute = new TestMethodAttribute();

        _testAssemblyInfo = new TestAssemblyInfo(typeof(DummyTestClass).Assembly);
        var testMethod = new TestMethod("dummyTestName", "dummyClassName", "dummyAssemblyName", false);
        _testContextImplementation = new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null!, "test"), new Dictionary<string, object?>());
        _testClassInfo = new TestClassInfo(typeof(DummyTestClass), _constructorInfo, true, _classAttribute, _testAssemblyInfo);
        _expectedException = new ExpectedExceptionAttribute(typeof(DivideByZeroException));

        _testMethodInfo = new TestMethodInfo(
            _methodInfo,
            parent: _testClassInfo,
            _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        // Reset test hooks
        DummyTestClass.TestConstructorMethodBody = () => { };
        DummyTestClass.TestContextSetterBody = value => { };
        DummyTestClass.TestInitializeMethodBody = value => { };
        DummyTestClass.TestMethodBody = instance => { };
        DummyTestClass.TestCleanupMethodBody = value => { };
    }

    public void SetArgumentsShouldSetArgumentsNeededForCurrentTestRun()
    {
        object[] arguments = [10, 20, 30];
        _testMethodInfo.SetArguments(arguments);

        Verify(_testMethodInfo.Arguments!.Length == 3);
        Verify((int?)_testMethodInfo.Arguments[0] == 10);
        Verify((int?)_testMethodInfo.Arguments[1] == 20);
        Verify((int?)_testMethodInfo.Arguments[2] == 30);
    }

    #region TestMethod invoke scenarios

    public void TestMethodInfoInvokeShouldWaitForAsyncTestMethodsToComplete()
    {
        bool methodCalled = false;
        DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => methodCalled = true);
        MethodInfo asyncMethodInfo = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod")!;

        var method = new TestMethodInfo(asyncMethodInfo, _testClassInfo, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        TestResult result = method.Invoke(null);

        Verify(methodCalled);
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void TestMethodInfoInvokeAsyncShouldHandleThrowAssertInconclusive()
    {
        DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => throw new AssertInconclusiveException());
        MethodInfo asyncMethodInfo = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod")!;

        var method = new TestMethodInfo(
            asyncMethodInfo,
            _testClassInfo,
            _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        TestResult result = method.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public void TestMethodInfoInvokeAsyncShouldHandleAssertInconclusive()
    {
        DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => UTF.Assert.Inconclusive());
        MethodInfo asyncMethodInfo = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod")!;

        var method = new TestMethodInfo(
            asyncMethodInfo,
            _testClassInfo,
            _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        TestResult result = method.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public void TestMethodInfoInvokeShouldHandleThrowAssertInconclusive()
    {
        DummyTestClass.TestMethodBody = d => throw new AssertInconclusiveException();
        MethodInfo dummyMethodInfo = typeof(DummyTestClass).GetMethod("DummyTestMethod")!;

        var method = new TestMethodInfo(
            dummyMethodInfo,
            _testClassInfo,
            _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        TestResult result = method.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public void TestMethodInfoInvokeShouldHandleAssertInconclusive()
    {
        DummyTestClass.TestMethodBody = d => UTF.Assert.Inconclusive();
        MethodInfo dummyMethodInfo = typeof(DummyTestClass).GetMethod("DummyTestMethod")!;

        var method = new TestMethodInfo(
            dummyMethodInfo,
            _testClassInfo,
            _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        TestResult result = method.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public void TestMethodInfoInvokeShouldReportTestContextMessages()
    {
        DummyTestClass.TestMethodBody = o => _testContextImplementation.WriteLine("TestContext");

        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        TestResult result = method.Invoke(null);

        Verify(result.TestContextMessages!.Contains("TestContext"));
    }

    public void TestMethodInfoInvokeShouldClearTestContextMessagesAfterReporting()
    {
        DummyTestClass.TestMethodBody = o => _testContextImplementation.WriteLine("TestContext");

        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        TestResult result = method.Invoke(null);

        Verify(result.TestContextMessages!.Contains("TestContext"));

        DummyTestClass.TestMethodBody = o => _testContextImplementation.WriteLine("SeaShore");

        result = method.Invoke(null);

        Verify(result.TestContextMessages!.Contains("SeaShore"));
    }

    public void Invoke_WhenTestMethodThrowsMissingMethodException_TestOutcomeIsFailedAndExceptionIsPreserved()
    {
        DummyTestClass.TestMethodBody = _ =>
        {
            var input = new
            {
                Field1 = "StringWith\0Null",
                Field2 = "NormalString",
            };

            Activator.CreateInstance(input.GetType());
        };

        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        TestResult result = method.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
        Verify(result.TestFailureException is TestFailedException);
        Verify(result.TestFailureException.InnerException is MissingMethodException);
    }

    #endregion

    #region TestClass constructor setup

    public void TestMethodInfoInvokeShouldCreateNewInstanceOfTestClassOnEveryCall()
    {
        int ctorCallCount = 0;
        DummyTestClass.TestConstructorMethodBody = () => ctorCallCount++;

        TestResult result = _testMethodInfo.Invoke(null);
        _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
        Verify(ctorCallCount == 2);
    }

    public void TestMethodInfoInvokeShouldMarkOutcomeFailedIfTestClassConstructorThrows()
    {
        DummyTestClass.TestConstructorMethodBody = () => throw new NotImplementedException();

        TestResult result = _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public void TestMethodInfoInvokeShouldSetErrorMessageIfTestClassConstructorThrows()
    {
        DummyTestClass.TestConstructorMethodBody = () => throw new NotImplementedException("dummyExceptionMessage");

        TestResult result = _testMethodInfo.Invoke(null);

        string errorMessage = string.Format(
            CultureInfo.InvariantCulture,
            Resource.UTA_InstanceCreationError,
            typeof(DummyTestClass).FullName,
            "System.NotImplementedException: dummyExceptionMessage");
        Verify(errorMessage == result.TestFailureException!.Message);
    }

    public void TestMethodInfoInvokeShouldSetErrorMessageIfTestClassConstructorThrowsWithoutInnerException()
    {
        ConstructorInfo ctorInfo = typeof(DummyTestClassWithParameterizedCtor).GetConstructors().Single();
        var testClass = new TestClassInfo(typeof(DummyTestClassWithParameterizedCtor), ctorInfo, true, _classAttribute, _testAssemblyInfo);
        var method = new TestMethodInfo(_methodInfo, testClass, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        TestResult result = method.Invoke(null);
        string errorMessage = string.Format(
            CultureInfo.InvariantCulture,
            Resource.UTA_InstanceCreationError,
            typeof(DummyTestClassWithParameterizedCtor).FullName,
            "System.Reflection.TargetParameterCountException: Parameter count mismatch.");

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
        Verify(errorMessage == result.TestFailureException!.Message);
    }

    public void TestMethodInfoInvokeShouldSetStackTraceInformationIfTestClassConstructorThrows()
    {
        DummyTestClass.TestConstructorMethodBody = () => throw new NotImplementedException("dummyExceptionMessage");

        var exception = _testMethodInfo.Invoke(null).TestFailureException as TestFailedException;

        Verify(exception is not null);
        Verify(exception.StackTraceInformation is not null);
        Verify(
            exception.StackTraceInformation.ErrorStackTrace.StartsWith(
                "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeShouldSetStackTraceInformationIfTestClassConstructorThrows>b__", StringComparison.Ordinal));
    }

    public void TestMethodInfoInvokeShouldSetStackTraceInformationIfTestClassConstructorThrowsWithoutInnerException()
    {
        ConstructorInfo ctorInfo = typeof(DummyTestClassWithParameterizedCtor).GetConstructors().Single();
        var testClass = new TestClassInfo(typeof(DummyTestClassWithParameterizedCtor), ctorInfo, true, _classAttribute, _testAssemblyInfo);
        var method = new TestMethodInfo(_methodInfo, testClass, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        var exception = method.Invoke(null).TestFailureException as TestFailedException;

        Verify(exception is not null);
        Verify(exception.StackTraceInformation is not null);
        Verify(
            exception.StackTraceInformation.ErrorStackTrace.StartsWith(
            "   at System.Reflection.RuntimeConstructorInfo.Invoke(BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)", StringComparison.Ordinal));
    }

    public void TestMethodInfoInvokeShouldSetResultFilesIfTestContextHasAttachments()
    {
        Mock<ITestContext> testContext = new();
        testContext.Setup(tc => tc.GetResultFiles()).Returns(["C:\\temp.txt"]);
        var mockInnerContext = new Mock<TestContext>();
        testContext.SetupGet(tc => tc.Context).Returns(mockInnerContext.Object);
        mockInnerContext.SetupGet(tc => tc.CancellationTokenSource).Returns(new CancellationTokenSource());

        var method = new TestMethodInfo(_methodInfo, _testClassInfo, testContext.Object)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        TestResult result = method.Invoke(null);
        Verify(result.ResultFiles!.Contains("C:\\temp.txt"));
    }

    public void TestMethodInfoInvoke_WhenCtorHasOneParameterOfTypeTestContext_SetsItToTestContext()
    {
        ConstructorInfo ctorInfo = typeof(DummyTestClass).GetConstructor([typeof(TestContext)])!;
        var testClassInfo = new TestClassInfo(typeof(DummyTestClass), ctorInfo, false, _classAttribute, _testAssemblyInfo);
        var testMethodInfo = new TestMethodInfo(_methodInfo, testClassInfo, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        TestResult result = testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    #endregion

    #region TestClass.TestContext property setup

    public void TestMethodInfoInvokeShouldNotThrowIfTestContextIsNotPresent()
    {
        var testClass = new TestClassInfo(typeof(DummyTestClass), _constructorInfo, true, _classAttribute, _testAssemblyInfo);
        var method = new TestMethodInfo(_methodInfo, testClass, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        TestResult result;
        void RunMethod() => result = method.Invoke(null);

        RunMethod();
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void TestMethodInfoInvokeShouldNotThrowIfTestContextDoesNotHaveASetter()
    {
        var testClass = new TestClassInfo(typeof(DummyTestClass), _constructorInfo, true, _classAttribute, _testAssemblyInfo);
        var method = new TestMethodInfo(_methodInfo, testClass, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        TestResult result;
        void RunMethod() => result = method.Invoke(null);

        RunMethod();
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void TestMethodInfoInvokeShouldSetTestContextForTestClassInstance()
    {
        TestContext? testContext = null;
        DummyTestClass.TestContextSetterBody = context => testContext = context as TestContext;

        _testMethodInfo.Invoke(null);

        Verify(_testContextImplementation.Equals(testContext));
    }

    public void TestMethodInfoInvokeShouldMarkOutcomeFailedIfSetTestContextThrows()
    {
        DummyTestClass.TestContextSetterBody = value => throw new NotImplementedException();

        TestResult result = _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public void TestMethodInfoInvokeShouldSetErrorMessageIfSetTestContextThrows()
    {
        DummyTestClass.TestContextSetterBody = value => throw new NotImplementedException("dummyExceptionMessage");

        var exception = _testMethodInfo.Invoke(null).TestFailureException as TestFailedException;

        string errorMessage = string.Format(
            CultureInfo.InvariantCulture,
            Resource.UTA_TestContextSetError,
            typeof(DummyTestClass).FullName,
            "System.NotImplementedException: dummyExceptionMessage");
        Verify(exception is not null);
        Verify(errorMessage == exception?.Message);
    }

    public void TestMethodInfoInvokeShouldSetStackTraceInformationIfSetTestContextThrows()
    {
        DummyTestClass.TestConstructorMethodBody = () => throw new NotImplementedException("dummyExceptionMessage");

        var exception = _testMethodInfo.Invoke(null).TestFailureException as TestFailedException;

        Verify(exception is not null);
        Verify(exception.StackTraceInformation is not null);
        Verify(
            exception.StackTraceInformation.ErrorStackTrace.StartsWith(
                "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeShouldSetStackTraceInformationIfSetTestContextThrows>b__", StringComparison.Ordinal));
    }

    public void TestMethodInfoInvoke_WhenCtorHasOneParameterOfTypeTestContextAndTestContextProperty_InitializeBothTestContexts()
    {
        ConstructorInfo ctorInfo = typeof(DummyTestClass).GetConstructor([typeof(TestContext)])!;
        var testClassInfo = new TestClassInfo(typeof(DummyTestClass), ctorInfo, false, _classAttribute, _testAssemblyInfo);
        var testMethodInfo = new TestMethodInfo(_methodInfo, testClassInfo, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };
        TestContext? testContext = null;
        DummyTestClass.TestContextSetterBody = context => testContext = context as TestContext;

        TestResult result = testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
        Verify(_testContextImplementation.Equals(testContext));
    }

    #endregion

    #region TestInitialize method setup

    public void TestMethodInfoInvokeShouldCallTestInitialize()
    {
        bool testInitializeCalled = false;
        DummyTestClass.TestInitializeMethodBody = classInstance => testInitializeCalled = true;
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod")!;

        TestResult result = _testMethodInfo.Invoke(null);

        Verify(testInitializeCalled);
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void TestMethodInfoInvokeShouldCallAsyncTestInitializeAndWaitForCompletion()
    {
        bool testInitializeCalled = false;
        DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => testInitializeCalled = true);
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod")!;

        TestResult result = _testMethodInfo.Invoke(null);

        Verify(testInitializeCalled);
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void TestMethodInfoInvokeShouldCallTestInitializeOfAllBaseClasses()
    {
        var callOrder = new List<string>();
        DummyTestClassBase.BaseTestClassMethodBody = classInstance => callOrder.Add("baseTestInitializeCalled2");
        DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => callOrder.Add("baseAsyncTestInitializeCalled1"));
        DummyTestClass.TestInitializeMethodBody = classInstance => callOrder.Add("classTestInitializeCalled");
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod")!;
        _testClassInfo.BaseTestInitializeMethodsQueue.Enqueue(typeof(DummyTestClassBase).GetMethod("DummyBaseTestClassMethod")!);
        _testClassInfo.BaseTestInitializeMethodsQueue.Enqueue(typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod")!);

        TestResult result = _testMethodInfo.Invoke(null);

        var expectedCallOrder = new List<string>
                                    {
                                        "baseAsyncTestInitializeCalled1",
                                        "baseTestInitializeCalled2",
                                        "classTestInitializeCalled",
                                    };
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
        Verify(expectedCallOrder.SequenceEqual(callOrder));
    }

    public void TestMethodInfoInvokeShouldNotThrowIfTestInitializeIsNull()
    {
        _testClassInfo.TestInitializeMethod = null;

        TestResult result = _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void TestMethodInfoInvokeShouldNotThrowIfTestInitializeForBaseClassIsNull()
    {
        _testClassInfo.BaseTestInitializeMethodsQueue.Enqueue(null!);

        TestResult result = _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void TestMethodInfoInvokeWhenTestThrowsReturnsExpectedResult()
    {
        // Arrange.
        DummyTestClass.TestInitializeMethodBody = classInstance => throw new ArgumentException("Some exception message", new InvalidOperationException("Inner exception message"));
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod")!;
        string errorMessage = string.Format(
            CultureInfo.InvariantCulture,
            Resource.UTA_InitMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestInitializeMethod!.Name,
            "System.ArgumentException: Some exception message ---> System.InvalidOperationException: Inner exception message");

        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
            ExpectedException = _expectedException,
        };

        // Act.
        TestResult result = testMethodInfo.Invoke(null);

        // Assert.
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);

        var exception = result.TestFailureException as TestFailedException;
        Verify(exception is not null);
        Verify(errorMessage == exception.Message);
        Verify(exception.Outcome == UTF.UnitTestOutcome.Failed);
        Verify(exception.InnerException!.GetType() == typeof(ArgumentException));
        Verify(exception.InnerException.InnerException!.GetType() == typeof(InvalidOperationException));
#if DEBUG
        Verify(exception.StackTraceInformation!.ErrorStackTrace.StartsWith(
    "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeWhenTestThrowsReturnsExpectedResult>b__", StringComparison.Ordinal));
#endif
    }

    public void TestInitialize_WhenTestReturnsTaskFromException_DisplayProperException()
    {
        // Arrange.
        DummyTestClass.TestInitializeMethodBodyAsync = async classInstance => await Task.FromException<Exception>(new Exception("Outer", new InvalidOperationException("Inner")));
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethodAsync")!;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
            ExpectedException = _expectedException,
        };

        // Act.
        TestResult result = testMethodInfo.Invoke(null);

        // Assert.
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);

        var exception = result.TestFailureException as TestFailedException;
        Verify(exception is not null);
        Verify(exception.Outcome == UTF.UnitTestOutcome.Failed);
        Verify(exception.InnerException!.GetType() == typeof(Exception));
        Verify(exception.InnerException.InnerException!.GetType() == typeof(InvalidOperationException));

        string expectedErrorMessage = string.Format(
            CultureInfo.InvariantCulture,
            Resource.UTA_InitMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestInitializeMethod!.Name,
            "System.Exception: Outer ---> System.InvalidOperationException: Inner");
        Verify(expectedErrorMessage == exception.Message);
        Verify(exception.StackTraceInformation is not null);
    }

    public void TestMethodInfoInvokeWhenTestThrowsAssertFailReturnsExpectedResult()
    {
        // Arrange.
        DummyTestClass.TestInitializeMethodBody = classInstance => UTF.Assert.Fail("dummyFailMessage");
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod")!;
        string errorMessage = string.Format(
            CultureInfo.InvariantCulture,
            Resource.UTA_InitMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestInitializeMethod!.Name,
            "Assert.Fail failed. dummyFailMessage");

        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
            ExpectedException = _expectedException,
        };

        // Act.
        TestResult result = testMethodInfo.Invoke(null);

        // Assert.
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);

        var exception = result.TestFailureException as TestFailedException;
        Verify(exception is not null);
        Verify(errorMessage == exception.Message);
        Verify(exception.Outcome == UTF.UnitTestOutcome.Failed);
        Verify(exception.InnerException!.GetType() == typeof(AssertFailedException));
#if DEBUG
        Verify(exception.StackTraceInformation!.ErrorStackTrace.StartsWith(
    "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeWhenTestThrowsAssertFailReturnsExpectedResult>b__", StringComparison.Ordinal));
#endif
    }

    public void TestMethodInfoInvokeWhenTestThrowsAssertInconclusiveReturnsExpectedResult()
    {
        // Arrange.
        DummyTestClass.TestInitializeMethodBody = classInstance => UTF.Assert.Inconclusive("dummyFailMessage");
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod")!;
        string errorMessage = string.Format(
            CultureInfo.InvariantCulture,
            Resource.UTA_InitMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestInitializeMethod!.Name,
            "Assert.Inconclusive failed. dummyFailMessage");

        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
            ExpectedException = _expectedException,
        };

        // Act.
        TestResult result = testMethodInfo.Invoke(null);

        // Assert.
        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);

        var exception = result.TestFailureException as TestFailedException;
        Verify(exception is not null);
        Verify(errorMessage == exception.Message);
        Verify(exception.Outcome == UTF.UnitTestOutcome.Inconclusive);
        Verify(exception.InnerException!.GetType() == typeof(AssertInconclusiveException));
#if DEBUG
        Verify(exception.StackTraceInformation!.ErrorStackTrace.StartsWith(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeWhenTestThrowsAssertInconclusiveReturnsExpectedResult>b__", StringComparison.Ordinal));
#endif
    }

    #endregion

    #region TestCleanup method setup

    public void TestCleanup_WhenTestReturnsTaskFromException_DisplayProperException()
    {
        // Arrange.
        DummyTestClass.TestCleanupMethodBodyAsync = async classInstance => await Task.FromException<Exception>(new Exception("Outer", new InvalidOperationException("Inner")));
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethodAsync")!;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        // Act.
        TestResult result = testMethodInfo.Invoke(null);

        // Assert.
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);

        var exception = result.TestFailureException as TestFailedException;
        Verify(exception is not null);
        Verify(exception.Outcome == UTF.UnitTestOutcome.Failed);
        Verify(exception.InnerException!.GetType() == typeof(Exception));
        Verify(exception.InnerException.InnerException!.GetType() == typeof(InvalidOperationException));

        string errorMessage = string.Format(
            CultureInfo.InvariantCulture,
            Resource.UTA_CleanupMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestCleanupMethod!.Name,
            "System.Exception: Outer ---> System.InvalidOperationException: Inner");
        Verify(errorMessage == exception.Message);

        if (exception.StackTraceInformation is null)
        {
            throw new Exception("Expected stack trace not to be empty.");
        }
    }

    public void TestMethodInfoInvokeShouldCallTestCleanup()
    {
        bool cleanupMethodCalled = false;
        DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => cleanupMethodCalled = true);
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod")!;

        TestResult result = _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
        Verify(cleanupMethodCalled);
    }

    public void TestMethodInfoInvokeShouldCallAsyncTestCleanup()
    {
        bool cleanupMethodCalled = false;
        DummyTestClass.TestCleanupMethodBody = classInstance => cleanupMethodCalled = true;
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod")!;

        TestResult result = _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
        Verify(cleanupMethodCalled);
    }

    public void TestMethodInfoInvokeShouldNotThrowIfTestCleanupMethodIsNull()
    {
        _testClassInfo.TestCleanupMethod = null;

        TestResult result = _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void TestMethodInfoInvokeShouldCallTestCleanupForBaseTestClasses()
    {
        var callOrder = new List<string>();
        DummyTestClassBase.BaseTestClassMethodBody = classInstance => callOrder.Add("baseTestCleanupCalled" + callOrder.Count);
        DummyTestClass.TestCleanupMethodBody = classInstance => callOrder.Add("classTestCleanupCalled");
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod")!;
        _testClassInfo.BaseTestCleanupMethodsQueue.Enqueue(typeof(DummyTestClassBase).GetMethod("DummyBaseTestClassMethod")!);
        _testClassInfo.BaseTestCleanupMethodsQueue.Enqueue(typeof(DummyTestClassBase).GetMethod("DummyBaseTestClassMethod")!);

        TestResult result = _testMethodInfo.Invoke(null);

        var expectedCallOrder = new List<string>
                                    {
                                        "classTestCleanupCalled",
                                        "baseTestCleanupCalled1",
                                        "baseTestCleanupCalled2",
                                    };
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
        Verify(expectedCallOrder.SequenceEqual(callOrder));
    }

    public void TestMethodInfoInvokeShouldCallTestCleanupForBaseTestClassesAlways()
    {
        var callOrder = new List<string>();
        DummyTestClassBase.BaseTestClassMethodBody = classInstance => callOrder.Add("baseTestCleanupCalled" + callOrder.Count);
        DummyTestClass.TestCleanupMethodBody = classInstance => callOrder.Add("classTestCleanupCalled");
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod")!;
        _testClassInfo.BaseTestCleanupMethodsQueue.Enqueue(typeof(DummyTestClassBase).GetMethod("DummyBaseTestClassMethod")!);
        _testClassInfo.BaseTestCleanupMethodsQueue.Enqueue(typeof(DummyTestClassBase).GetMethod("DummyBaseTestClassMethod")!);

        _testMethodInfo.Invoke(null);
        TestResult result = _testMethodInfo.Invoke(null);

        var expectedCallOrder = new List<string>
                                    {
                                        "classTestCleanupCalled",
                                        "baseTestCleanupCalled1",
                                        "baseTestCleanupCalled2",
                                        "classTestCleanupCalled",
                                        "baseTestCleanupCalled4",
                                        "baseTestCleanupCalled5",
                                    };

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
        Verify(expectedCallOrder.SequenceEqual(callOrder));
    }

    public void TestMethodInfoInvokeWhenTestCleanupThrowsReturnsExpectedResult()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => throw new ArgumentException("Some exception message", new InvalidOperationException("Inner exception message"));
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod")!;

        string expectedErrorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_CleanupMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestCleanupMethod!.Name,
            "System.ArgumentException: Some exception message ---> System.InvalidOperationException: Inner exception message");

        TestResult result = _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);

        var exception = result.TestFailureException as TestFailedException;
        Verify(exception is not null);
        Verify(exception.Outcome == UTF.UnitTestOutcome.Failed);
        Verify(expectedErrorMessage == exception.Message);
        Verify(exception.InnerException!.GetType() == typeof(ArgumentException));
        Verify(exception.InnerException.InnerException!.GetType() == typeof(InvalidOperationException));

#if DEBUG
        Verify(exception.StackTraceInformation!.ErrorStackTrace.StartsWith(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeWhenTestCleanupThrowsReturnsExpectedResult>b__", StringComparison.Ordinal));
#endif
    }

    public void TestMethodInfoInvokeWhenTestCleanupThrowsAssertInconclusiveReturnsExpectedResult()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => UTF.Assert.Inconclusive("Test inconclusive");
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod")!;

        string expectedErrorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_CleanupMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestCleanupMethod.Name,
            "Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException: Assert.Inconclusive failed. Test inconclusive");

        TestResult result = _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);

        var exception = result.TestFailureException as TestFailedException;
        Verify(exception is not null);
        Verify(exception.Outcome == UTF.UnitTestOutcome.Inconclusive);
        Verify(expectedErrorMessage == exception.Message);
        Verify(exception.InnerException!.GetType() == typeof(AssertInconclusiveException));
#if DEBUG
        Verify(exception.StackTraceInformation!.ErrorStackTrace.StartsWith(
    "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeWhenTestCleanupThrowsAssertInconclusiveReturnsExpectedResult>b__", StringComparison.Ordinal));
#endif
    }

    public void TestMethodInfoInvokeWhenTestCleanupThrowsAssertFailedReturnsExpectedResult()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => UTF.Assert.Fail("Test failed");
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod")!;

        string expectedErrorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_CleanupMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestCleanupMethod!.Name,
            "Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException: Assert.Fail failed. Test failed");

        TestResult result = _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);

        var exception = result.TestFailureException as TestFailedException;
        Verify(exception is not null);
        Verify(exception.Outcome == UTF.UnitTestOutcome.Failed);
        Verify(expectedErrorMessage == exception.Message);
        Verify(exception.InnerException!.GetType() == typeof(AssertFailedException));
#if DEBUG
        Verify(exception.StackTraceInformation!.ErrorStackTrace.StartsWith(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeWhenTestCleanupThrowsAssertFailedReturnsExpectedResult>b__", StringComparison.Ordinal));
#endif
    }

    public void TestMethodInfoInvokeShouldAppendErrorMessagesIfBothTestMethodAndTestCleanupThrows()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => throw new NotImplementedException("dummyErrorMessage");
        DummyTestClass.TestMethodBody = classInstance => throw new NotImplementedException("dummyMethodError");
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod")!;

        TestResult result = _testMethodInfo.Invoke(null);
        var exception = result.TestFailureException as AggregateException;
        string errorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_TestMethodThrows,
            typeof(DummyTestClass).FullName,
            _testMethodInfo.TestMethodName,
            "System.NotImplementedException: dummyMethodError");
        string cleanupError = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_CleanupMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestCleanupMethod!.Name,
            "System.NotImplementedException: dummyErrorMessage");

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
        Verify(exception is not null);
        Verify(exception.InnerExceptions[0].Message.Contains(errorMessage));
        Verify(exception.InnerExceptions[1].Message.Contains(cleanupError));
    }

    public void TestMethodInfoInvokeShouldAppendStackTraceInformationIfBothTestMethodAndTestCleanupThrows()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => throw new NotImplementedException();
        DummyTestClass.TestMethodBody = classInstance => throw new NotImplementedException("dummyMethodError");
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod")!;

        TestResult result = _testMethodInfo.Invoke(null);
        var exception = result.TestFailureException as AggregateException;

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
        Verify(exception is not null);
#if DEBUG
        Verify(((TestFailedException)exception.InnerExceptions[0]).StackTraceInformation!.ErrorStackTrace.Contains("Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.DummyTestClass.DummyTestMethod()"));
        Verify(((TestFailedException)exception.InnerExceptions[1]).StackTraceInformation!.ErrorStackTrace.Contains("Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.DummyTestClass.DummyTestCleanupMethod()"));
#endif
    }

    public void TestMethodInfoInvokeShouldSetOutcomeAsInconclusiveIfTestCleanupIsInconclusive()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => throw new AssertInconclusiveException();
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod")!;

        TestResult result = _testMethodInfo.Invoke(null);
        var exception = result.TestFailureException as TestFailedException;

        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
        Verify(exception is not null);
        Verify(exception.Message.Contains("Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException"));
    }

    public void TestMethodInfoInvokeShouldSetMoreImportantOutcomeIfTestCleanupIsInconclusiveButTestMethodFails()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => throw new AssertInconclusiveException();
        DummyTestClass.TestMethodBody = classInstance => Fail();
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod")!;

        TestResult result = _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public void TestMethodInfoInvokeShouldCallDisposeForDisposableTestClass()
    {
        bool disposeCalled = false;
        DummyTestClassWithDisposable.DisposeMethodBody = () => disposeCalled = true;
        ConstructorInfo ctorInfo = typeof(DummyTestClassWithDisposable).GetConstructor([])!;
        var testClass = new TestClassInfo(typeof(DummyTestClassWithDisposable), ctorInfo, true, _classAttribute, _testAssemblyInfo);
        var method = new TestMethodInfo(typeof(DummyTestClassWithDisposable).GetMethod("DummyTestMethod")!, testClass, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        method.Invoke(null);

        Verify(disposeCalled);
    }

#if NET6_0_OR_GREATER
    public void TestMethodInfoInvoke_WhenTestClassIsAsyncDisposable_ShouldDisposeAsync()
    {
        // Arrange
        bool asyncDisposeCalled = false;
        DummyTestClassWithAsyncDisposable.DisposeAsyncMethodBody = () => asyncDisposeCalled = true;
        ConstructorInfo ctorInfo = typeof(DummyTestClassWithAsyncDisposable).GetConstructor([])!;
        var testClass = new TestClassInfo(typeof(DummyTestClassWithAsyncDisposable), ctorInfo, true, _classAttribute, _testAssemblyInfo);
        var method = new TestMethodInfo(typeof(DummyTestClassWithAsyncDisposable).GetMethod("DummyTestMethod")!, testClass, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        // Act
        method.Invoke(null);

        // Assert
        Verify(asyncDisposeCalled);
    }

    public void TestMethodInfoInvoke_WhenTestClassIsDisposableAndAsyncDisposable_ShouldCallAsyncDisposeThenDispose()
    {
        // Arrange
        int order = 0;
        int disposeCalledOrder = 0;
        int disposeAsyncCalledOrder = 0;

        DummyTestClassWithAsyncDisposableAndDisposable.DisposeMethodBody = () => disposeCalledOrder = ++order;
        DummyTestClassWithAsyncDisposableAndDisposable.DisposeAsyncMethodBody = () => disposeAsyncCalledOrder = ++order;

        ConstructorInfo ctorInfo = typeof(DummyTestClassWithAsyncDisposableAndDisposable).GetConstructor([])!;
        var testClass = new TestClassInfo(typeof(DummyTestClassWithAsyncDisposableAndDisposable), ctorInfo, true, _classAttribute, _testAssemblyInfo);
        var method = new TestMethodInfo(typeof(DummyTestClassWithAsyncDisposableAndDisposable).GetMethod("DummyTestMethod")!, testClass, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        // Act
        method.Invoke(null);

        // Assert
        Verify(disposeCalledOrder == 2);
        Verify(disposeAsyncCalledOrder == 1);
    }
#endif

    public void TestMethodInfoInvokeShouldCallDisposeForDisposableTestClassIfTestCleanupThrows()
    {
        bool disposeCalled = false;
        DummyTestClassWithDisposable.DisposeMethodBody = () => disposeCalled = true;
        DummyTestClassWithDisposable.DummyTestCleanupMethodBody = classInstance => throw new NotImplementedException();
        ConstructorInfo ctorInfo = typeof(DummyTestClassWithDisposable).GetConstructor([])!;
        var testClass = new TestClassInfo(typeof(DummyTestClassWithDisposable), ctorInfo, true, _classAttribute, _testAssemblyInfo)
        {
            TestCleanupMethod = typeof(DummyTestClassWithDisposable).GetMethod("DummyTestCleanupMethod")!,
        };
        var method = new TestMethodInfo(typeof(DummyTestClassWithDisposable).GetMethod("DummyTestMethod")!, testClass, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        method.Invoke(null);

        Verify(disposeCalled);
    }

    public void TestMethodInfoInvokeShouldCallTestCleanupEvenIfTestMethodThrows()
    {
        bool testCleanupMethodCalled = false;
        DummyTestClass.TestMethodBody = classInstance => throw new NotImplementedException();
        DummyTestClass.TestCleanupMethodBody = classInstance => testCleanupMethodCalled = true;
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod")!;

        TestResult result = _testMethodInfo.Invoke(null);

        Verify(testCleanupMethodCalled);
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public void TestMethodInfoInvokeShouldCallTestCleanupEvenIfTestInitializeMethodThrows()
    {
        bool testCleanupMethodCalled = false;
        DummyTestClass.TestInitializeMethodBody = classInstance => throw new NotImplementedException();
        DummyTestClass.TestCleanupMethodBody = classInstance => testCleanupMethodCalled = true;
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod")!;
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod")!;

        TestResult result = _testMethodInfo.Invoke(null);

        Verify(testCleanupMethodCalled);
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public void TestMethodInfoInvokeShouldCallTestCleanupIfTestClassInstanceIsNotNull()
    {
        bool testCleanupMethodCalled = false;

        // Throwing in constructor to ensure classInstance is null in TestMethodInfo.Invoke
        DummyTestClass.TestConstructorMethodBody = () => throw new NotImplementedException();
        DummyTestClass.TestCleanupMethodBody = classInstance => testCleanupMethodCalled = true;

        TestResult result = _testMethodInfo.Invoke(null);

        Verify(!testCleanupMethodCalled);
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public void TestMethodInfoInvokeShouldNotCallTestCleanupIfClassSetContextThrows()
    {
        bool testCleanupCalled = false;
        DummyTestClass.TestCleanupMethodBody = classInstance => testCleanupCalled = true;
        DummyTestClass.TestContextSetterBody = o => throw new NotImplementedException();
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod")!;

        _testMethodInfo.Invoke(null);

        Verify(!testCleanupCalled);
    }

    public void TestMethodInfoInvokeShouldSetResultAsPassedIfExpectedExceptionIsThrown()
    {
        DummyTestClass.TestMethodBody = o => throw new DivideByZeroException();
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
            ExpectedException = _expectedException,
        };

        TestResult result = testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void TestMethodInfoInvokeShouldSetResultAsFailedIfExceptionDifferentFromExpectedExceptionIsThrown()
    {
        DummyTestClass.TestMethodBody = o => throw new IndexOutOfRangeException();
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
            ExpectedException = _expectedException,
        };

        TestResult result = testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
        string message = "Test method threw exception System.IndexOutOfRangeException, but exception System.DivideByZeroException was expected. " +
            "Exception message: System.IndexOutOfRangeException: Index was outside the bounds of the array.";
        Verify(message == result.TestFailureException!.Message);
    }

    public void TestMethodInfoInvokeShouldSetResultAsFailedWhenExceptionIsExpectedButIsNotThrown()
    {
        DummyTestClass.TestMethodBody = o => { };
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
            ExpectedException = _expectedException,
        };
        TestResult result = testMethodInfo.Invoke(null);
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
        string message = "Test method did not throw expected exception System.DivideByZeroException.";
        Verify(result.TestFailureException!.Message.Contains(message));
    }

    public void TestMethodInfoInvokeShouldSetResultAsInconclusiveWhenExceptionIsAssertInconclusiveException()
    {
        DummyTestClass.TestMethodBody = o => throw new AssertInconclusiveException();
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
            ExpectedException = _expectedException,
        };
        TestResult result = testMethodInfo.Invoke(null);
        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
        string message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException' was thrown.";
        Verify(message == result.TestFailureException!.Message);
    }

    public void TestMethodInfoInvokeShouldSetTestOutcomeBeforeTestCleanup()
    {
        UTF.UnitTestOutcome testOutcome = UTF.UnitTestOutcome.Unknown;
        DummyTestClass.TestMethodBody = o => throw new AssertInconclusiveException();
        DummyTestClass.TestCleanupMethodBody = c =>
        {
            if (DummyTestClass.GetTestContext() != null)
            {
                testOutcome = DummyTestClass.GetTestContext().CurrentTestOutcome;
            }
        };
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod")!;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
            ExpectedException = _expectedException,
        };

        TestResult result = testMethodInfo.Invoke(null);

        Verify(testOutcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public void HandleMethodExceptionShouldInvokeVerifyOfCustomExpectedException()
    {
        CustomExpectedExceptionAttribute customExpectedException = new(typeof(DivideByZeroException), "Attempted to divide by zero");
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(0),
            Executor = _testMethodAttribute,
            ExpectedException = customExpectedException,
        };

        DummyTestClass.TestMethodBody = o => throw new DivideByZeroException();
        TestResult result = method.Invoke(null);
        Verify(customExpectedException.IsVerifyInvoked);
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void HandleMethodExceptionShouldSetOutcomeAsFailedIfVerifyOfExpectedExceptionThrows()
    {
        CustomExpectedExceptionAttribute customExpectedException = new(typeof(DivideByZeroException), "Custom Exception");
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(0),
            Executor = _testMethodAttribute,
            ExpectedException = customExpectedException,
        };

        DummyTestClass.TestMethodBody = o => throw new DivideByZeroException();
        TestResult result = method.Invoke(null);
        Verify(result.TestFailureException!.Message == "The exception message doesn't contain the string defined in the exception attribute");
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public void HandleMethodExceptionShouldSetOutcomeAsInconclusiveIfVerifyOfExpectedExceptionThrowsAssertInconclusiveException()
    {
        CustomExpectedExceptionAttribute customExpectedException = new(typeof(DivideByZeroException), "Custom Exception");
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(0),
            Executor = _testMethodAttribute,
            ExpectedException = customExpectedException,
        };

        DummyTestClass.TestMethodBody = o => throw new AssertInconclusiveException();
        TestResult result = method.Invoke(null);
        string message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException' was thrown.";
        Verify(result.TestFailureException!.Message == message);
        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public void HandleMethodExceptionShouldInvokeVerifyOfDerivedCustomExpectedException()
    {
        DerivedCustomExpectedExceptionAttribute derivedCustomExpectedException = new(typeof(DivideByZeroException), "Attempted to divide by zero");
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(0),
            Executor = _testMethodAttribute,
            ExpectedException = derivedCustomExpectedException,
        };

        DummyTestClass.TestMethodBody = o => throw new DivideByZeroException();
        TestResult result = method.Invoke(null);
        Verify(derivedCustomExpectedException.IsVerifyInvoked);
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void VerifyShouldNotThrowIfThrownExceptionCanBeAssignedToExpectedException()
    {
        ExpectedExceptionAttribute expectedException = new(typeof(Exception))
        {
            AllowDerivedTypes = true,
        };
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(0),
            Executor = _testMethodAttribute,
            ExpectedException = expectedException,
        };

        DummyTestClass.TestMethodBody = o => throw new DivideByZeroException();
        TestResult result = method.Invoke(null);
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void VerifyShouldThrowExceptionIfThrownExceptionCannotBeAssignedToExpectedException()
    {
        ExpectedExceptionAttribute expectedException = new(typeof(DivideByZeroException), "Custom Exception")
        {
            AllowDerivedTypes = true,
        };
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(0),
            Executor = _testMethodAttribute,
            ExpectedException = expectedException,
        };

        DummyTestClass.TestMethodBody = o => throw new ArgumentNullException();
        TestResult result = method.Invoke(null);
        string message = "Test method threw exception System.ArgumentNullException, but exception System.DivideByZeroException" +
            " or a type derived from it was expected. Exception message: System.ArgumentNullException: Value cannot be null.";
        Verify(result.TestFailureException!.Message == message);
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public void VerifyShouldRethrowExceptionIfThrownExceptionIsAssertFailedException()
    {
        ExpectedExceptionAttribute expectedException = new(typeof(DivideByZeroException))
        {
            AllowDerivedTypes = true,
        };
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(0),
            Executor = _testMethodAttribute,
            ExpectedException = expectedException,
        };

        DummyTestClass.TestMethodBody = o => throw new AssertFailedException();
        TestResult result = method.Invoke(null);
        string message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException' was thrown.";
        Verify(result.TestFailureException!.Message == message);
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public void VerifyShouldRethrowExceptionIfThrownExceptionIsAssertInconclusiveException()
    {
        ExpectedExceptionAttribute expectedException = new(typeof(DivideByZeroException))
        {
            AllowDerivedTypes = true,
        };
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(0),
            Executor = _testMethodAttribute,
            ExpectedException = expectedException,
        };

        DummyTestClass.TestMethodBody = o => throw new AssertInconclusiveException();
        TestResult result = method.Invoke(null);
        string message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException' was thrown.";
        Verify(result.TestFailureException!.Message == message);
        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public void VerifyShouldThrowIfThrownExceptionIsNotSameAsExpectedException()
    {
        ExpectedExceptionAttribute expectedException = new(typeof(Exception));
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(0),
            Executor = _testMethodAttribute,
            ExpectedException = expectedException,
        };

        DummyTestClass.TestMethodBody = o => throw new DivideByZeroException();
        TestResult result = method.Invoke(null);
        string message = "Test method threw exception System.DivideByZeroException, but exception System.Exception was expected. " +
            "Exception message: System.DivideByZeroException: Attempted to divide by zero.";
        Verify(result.TestFailureException!.Message == message);
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public void VerifyShouldRethrowIfThrownExceptionIsAssertExceptionWhichIsNotSameAsExpectedException()
    {
        ExpectedExceptionAttribute expectedException = new(typeof(Exception));
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(0),
            Executor = _testMethodAttribute,
            ExpectedException = expectedException,
        };

        DummyTestClass.TestMethodBody = o => throw new AssertInconclusiveException();
        TestResult result = method.Invoke(null);
        string message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException' was thrown.";
        Verify(result.TestFailureException!.Message == message);
        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public void ResolveExpectedExceptionShouldThrowWhenAttributeIsDefinedTwice_DifferentConcreteType()
    {
        MethodInfo testMethodInfo = typeof(DummyTestClassForExpectedException).GetMethod(nameof(DummyTestClassForExpectedException.DummyTestMethod1))!;
        TestClassInfo classInfo = new(
            typeof(DummyTestClassForExpectedException),
            typeof(DummyTestClassForExpectedException).GetConstructor([])!,
            isParameterlessConstructor: true,
            new TestClassAttribute(),
            new TestAssemblyInfo(typeof(DummyTestClassForExpectedException).Assembly));

        TypeInspectionException ex = UTF.Assert.ThrowsException<TypeInspectionException>(() => new TestMethodInfo(testMethodInfo, classInfo, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        });
        UTF.Assert.AreEqual("The test method 'Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests+DummyTestClassForExpectedException.DummyTestMethod1' has multiple attributes derived from 'ExpectedExceptionBaseAttribute' defined on it. Only one such attribute is allowed.", ex.Message);
    }

    public void ResolveExpectedExceptionShouldThrowWhenAttributeIsDefinedTwice_SameConcreteType()
    {
        MethodInfo testMethodInfo = typeof(DummyTestClassForExpectedException).GetMethod(nameof(DummyTestClassForExpectedException.DummyTestMethod1))!;
        TestClassInfo classInfo = new(
            typeof(DummyTestClassForExpectedException),
            typeof(DummyTestClassForExpectedException).GetConstructor([])!,
            isParameterlessConstructor: true,
            new TestClassAttribute(),
            new TestAssemblyInfo(typeof(DummyTestClassForExpectedException).Assembly));

        TypeInspectionException ex = UTF.Assert.ThrowsException<TypeInspectionException>(() => new TestMethodInfo(testMethodInfo, classInfo, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        });
        UTF.Assert.AreEqual("The test method 'Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests+DummyTestClassForExpectedException.DummyTestMethod1' has multiple attributes derived from 'ExpectedExceptionBaseAttribute' defined on it. Only one such attribute is allowed.", ex.Message);
    }

    public void ResolveExpectedExceptionHelperShouldReturnExpectedExceptionAttributeIfPresent()
    {
        Type type = typeof(DummyTestClassForExpectedException);
        MethodInfo methodInfo = type.GetMethod(nameof(DummyTestClassForExpectedException.TestMethodWithExpectedException))!;
        TestClassInfo classInfo = new(
            typeof(DummyTestClassForExpectedException),
            typeof(DummyTestClassForExpectedException).GetConstructor([])!,
            isParameterlessConstructor: true,
            new TestClassAttribute(),
            new TestAssemblyInfo(typeof(DummyTestClassForExpectedException).Assembly));

        var testMethodInfo = new TestMethodInfo(methodInfo, classInfo, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        Verify(testMethodInfo.ExpectedException is not null);
        Verify(((ExpectedExceptionAttribute)testMethodInfo.ExpectedException).ExceptionType == typeof(DivideByZeroException));
    }

    public void ResolveExpectedExceptionHelperShouldReturnNullIfExpectedExceptionAttributeIsNotPresent()
    {
        Type type = typeof(DummyTestClassForExpectedException);
        MethodInfo methodInfo = type.GetMethod(nameof(DummyTestClassForExpectedException.TestMethodWithoutExpectedException))!;
        TestClassInfo classInfo = new(
            typeof(DummyTestClassForExpectedException),
            typeof(DummyTestClassForExpectedException).GetConstructor([])!,
            isParameterlessConstructor: true,
            new TestClassAttribute(),
            new TestAssemblyInfo(typeof(DummyTestClassForExpectedException).Assembly));

        var testMethodInfo = new TestMethodInfo(methodInfo, classInfo, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        Verify(testMethodInfo.ExpectedException is null);
    }

    #endregion

    #region TestMethod invoke setup order

    public void TestMethodInfoInvokeShouldInitializeClassInstanceTestInitializeAndTestCleanupInOrder()
    {
        var callOrder = new List<string>();
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod")!;
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod")!;

        DummyTestClass.TestConstructorMethodBody = () => callOrder.Add("classCtor");
        DummyTestClass.TestContextSetterBody = o => callOrder.Add("testContext");
        DummyTestClass.TestInitializeMethodBody = classInstance => callOrder.Add("testInit");
        DummyTestClass.TestMethodBody = classInstance => callOrder.Add("testMethodInfo");
        DummyTestClass.TestCleanupMethodBody = classInstance => callOrder.Add("testCleanup");

        TestResult result = _testMethodInfo.Invoke(null);

        var expectedCallOrder = new List<string>
                                    {
                                        "classCtor",
                                        "testContext",
                                        "testInit",
                                        "testMethodInfo",
                                        "testCleanup",
                                    };
        Verify(expectedCallOrder.SequenceEqual(callOrder));
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    #endregion

    #region TestMethod timeout scenarios

    public void TestMethodInfoInvokeShouldReturnTestFailureOnTimeout()
    {
        var testablePlatformServiceProvider = new TestablePlatformServiceProvider();

        RunWithTestablePlatformService(testablePlatformServiceProvider, () =>
        {
            testablePlatformServiceProvider.MockThreadOperations.CallBase = true;

            PlatformServiceProvider.Instance = testablePlatformServiceProvider;

            testablePlatformServiceProvider.MockThreadOperations.Setup(
             to => to.Execute(It.IsAny<Action>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(false);
            var method = new TestMethodInfo(_methodInfo, _testClassInfo, _testContextImplementation)
            {
                TimeoutInfo = TimeoutInfo.FromTimeout(1),
                Executor = _testMethodAttribute,
            };

            TestResult result = method.Invoke(null);

            Verify(result.Outcome == UTF.UnitTestOutcome.Timeout);
            Verify(result.TestFailureException!.Message.Equals("Test 'DummyTestMethod' timed out after 1ms", StringComparison.Ordinal));
        });
    }

    public void TestMethodInfoInvokeShouldReturnTestPassedOnCompletionWithinTimeout()
    {
        DummyTestClass.TestMethodBody = o => { /* do nothing */ };
        var method = new TestMethodInfo(_methodInfo, _testClassInfo, _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };
        TestResult result = method.Invoke(null);
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void TestMethodInfoInvokeShouldCancelTokenSourceOnTimeout()
    {
        var testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        RunWithTestablePlatformService(testablePlatformServiceProvider, () =>
        {
            testablePlatformServiceProvider.MockThreadOperations.CallBase = true;
            PlatformServiceProvider.Instance = testablePlatformServiceProvider;

            testablePlatformServiceProvider.MockThreadOperations.Setup(
             to => to.Execute(It.IsAny<Action>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(false);

            var method = new TestMethodInfo(_methodInfo, _testClassInfo, _testContextImplementation)
            {
                TimeoutInfo = TimeoutInfo.FromTimeout(1),
                Executor = _testMethodAttribute,
            };
            TestResult result = method.Invoke(null);

            Verify(result.Outcome == UTF.UnitTestOutcome.Timeout);
            Verify(result.TestFailureException!.Message.Equals("Test 'DummyTestMethod' timed out after 1ms", StringComparison.Ordinal));
            Verify(_testContextImplementation.CancellationTokenSource.IsCancellationRequested, "Not canceled..");
        });
    }

    public void TestMethodInfoInvokeShouldFailOnTokenSourceCancellation()
    {
        var testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        RunWithTestablePlatformService(testablePlatformServiceProvider, () =>
        {
            testablePlatformServiceProvider.MockThreadOperations.CallBase = true;
            PlatformServiceProvider.Instance = testablePlatformServiceProvider;

            testablePlatformServiceProvider.MockThreadOperations.Setup(
             to => to.Execute(It.IsAny<Action>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).Callback((Action action, int timeout, CancellationToken cancelToken) =>
             {
                 try
                 {
                     Task.WaitAny([Task.Delay(100000, cancelToken)], cancelToken);
                 }
                 catch (OperationCanceledException)
                 {
                 }
             });

            _testContextImplementation.CancellationTokenSource.CancelAfter(100);
            var method = new TestMethodInfo(_methodInfo, _testClassInfo, _testContextImplementation)
            {
                TimeoutInfo = TimeoutInfo.FromTimeout(100000),
                Executor = _testMethodAttribute,
            };
            TestResult result = method.Invoke(null);

            Verify(result.Outcome == UTF.UnitTestOutcome.Timeout);
            Verify(result.TestFailureException!.Message.Equals("Test 'DummyTestMethod' was canceled", StringComparison.Ordinal));
            Verify(_testContextImplementation.CancellationTokenSource.IsCancellationRequested, "Not canceled..");
        });
    }

    #endregion

    public void ResolveArgumentsShouldReturnProvidedArgumentsWhenTooFewParameters()
    {
        MethodInfo simpleArgumentsMethod = typeof(DummyTestClass).GetMethod("DummySimpleArgumentsMethod")!;

        var method = new TestMethodInfo(
            simpleArgumentsMethod,
            _testClassInfo,
            _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        object[] arguments = ["RequiredStr1"];
        object[] expectedArguments = ["RequiredStr1"];
        object?[] resolvedArguments = method.ResolveArguments(arguments);

        Verify(resolvedArguments.Length == 1);
        Verify(expectedArguments.SequenceEqual(resolvedArguments));
    }

    public void ResolveArgumentsShouldReturnProvidedArgumentsWhenTooManyParameters()
    {
        MethodInfo simpleArgumentsMethod = typeof(DummyTestClass).GetMethod("DummySimpleArgumentsMethod")!;

        var method = new TestMethodInfo(
            simpleArgumentsMethod,
            _testClassInfo,
            _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        object?[] arguments = ["RequiredStr1", "RequiredStr2", "ExtraStr3"];
        object?[] expectedArguments = ["RequiredStr1", "RequiredStr2", "ExtraStr3"];
        object?[] resolvedArguments = method.ResolveArguments(arguments);

        Verify(resolvedArguments.Length == 3);
        Verify(expectedArguments.SequenceEqual(resolvedArguments));
    }

    public void ResolveArgumentsShouldReturnAdditionalOptionalParametersWithNoneProvided()
    {
        MethodInfo optionalArgumentsMethod = typeof(DummyTestClass).GetMethod("DummyOptionalArgumentsMethod")!;

        var method = new TestMethodInfo(
            optionalArgumentsMethod,
            _testClassInfo,
            _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        object?[] arguments = ["RequiredStr1"];
        object?[] expectedArguments = ["RequiredStr1", null, null];
        object?[] resolvedArguments = method.ResolveArguments(arguments);

        Verify(resolvedArguments.Length == 3);
        Verify(expectedArguments.SequenceEqual(resolvedArguments));
    }

    public void ResolveArgumentsShouldReturnAdditionalOptionalParametersWithSomeProvided()
    {
        MethodInfo optionalArgumentsMethod = typeof(DummyTestClass).GetMethod("DummyOptionalArgumentsMethod")!;

        var method = new TestMethodInfo(
            optionalArgumentsMethod,
            _testClassInfo,
            _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        object?[] arguments = ["RequiredStr1", "OptionalStr1"];
        object?[] expectedArguments = ["RequiredStr1", "OptionalStr1", null];
        object?[] resolvedArguments = method.ResolveArguments(arguments);

        Verify(resolvedArguments.Length == 3);
        Verify(expectedArguments.SequenceEqual(resolvedArguments));
    }

    public void ResolveArgumentsShouldReturnEmptyParamsWithNoneProvided()
    {
        MethodInfo paramsArgumentMethod = typeof(DummyTestClass).GetMethod("DummyParamsArgumentMethod")!;

        var method = new TestMethodInfo(
            paramsArgumentMethod,
            _testClassInfo,
            _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        object[] arguments = [1];
        object[] expectedArguments = [1, Array.Empty<string>()];
        object?[] resolvedArguments = method.ResolveArguments(arguments);

        Verify(resolvedArguments.Length == 2);
        Verify(expectedArguments[0].Equals(resolvedArguments[0]));
        Verify(resolvedArguments[1]!.GetType() == typeof(string[]));
        Verify(((string[])expectedArguments[1]).SequenceEqual((string[])resolvedArguments[1]!));
    }

    public void ResolveArgumentsShouldReturnPopulatedParamsWithAllProvided()
    {
        MethodInfo paramsArgumentMethod = typeof(DummyTestClass).GetMethod("DummyParamsArgumentMethod")!;

        var method = new TestMethodInfo(
            paramsArgumentMethod,
            _testClassInfo,
            _testContextImplementation)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
            Executor = _testMethodAttribute,
        };

        object[] arguments = [1, "str1", "str2", "str3"];
        object[] expectedArguments = [1, new string[] { "str1", "str2", "str3" }];
        object?[] resolvedArguments = method.ResolveArguments(arguments);

        Verify(resolvedArguments.Length == 2);
        Verify(expectedArguments[0].Equals(resolvedArguments[0]));
        Verify(resolvedArguments[1]!.GetType() == typeof(string[]));
        Verify(((string[])expectedArguments[1]).SequenceEqual((string[])resolvedArguments[1]!));
    }

    #region helper methods

    private static void RunWithTestablePlatformService(TestablePlatformServiceProvider testablePlatformServiceProvider, Action action)
    {
        try
        {
            testablePlatformServiceProvider.MockThreadOperations.
                Setup(tho => tho.Execute(It.IsAny<Action>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).
                Returns(true).
                Callback((Action a, int timeout, CancellationToken token) => a.Invoke());

            action.Invoke();
        }
        finally
        {
            PlatformServiceProvider.Instance = null;
        }
    }

    #endregion

    #region Test data
    public class DummyTestClassBase
    {
        public static Action<DummyTestClassBase> BaseTestClassMethodBody { get; set; } = null!;

        public void DummyBaseTestClassMethod() => BaseTestClassMethodBody(this);
    }

    public class DummyTestClass : DummyTestClassBase
    {
        private static TestContext s_tc = null!;

        public DummyTestClass() => TestConstructorMethodBody();

        public DummyTestClass(TestContext tc) => Verify(tc is not null);

        public static Action TestConstructorMethodBody { get; set; } = null!;

        public static Action<object> TestContextSetterBody { get; set; } = null!;

        public static Func<DummyTestClass, Task> TestInitializeMethodBodyAsync { get; set; } = null!;

        public static Action<DummyTestClass> TestInitializeMethodBody { get; set; } = null!;

        public static Action<DummyTestClass> TestMethodBody { get; set; } = null!;

        public static Action<DummyTestClass> TestCleanupMethodBody { get; set; } = null!;

        public static Func<DummyTestClass, Task> TestCleanupMethodBodyAsync { get; set; } = null!;

        public static Func<Task> DummyAsyncTestMethodBody { get; set; } = null!;

        public static TestContext GetTestContext() => s_tc;

        public TestContext TestContext
        {
            get => throw new NotImplementedException();

            set
            {
                TestContextSetterBody(value);
                s_tc = value;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void DummyTestInitializeMethod() => TestInitializeMethodBody(this);

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public async Task DummyTestInitializeMethodAsync() => await TestInitializeMethodBodyAsync(this);

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void DummyTestCleanupMethod() => TestCleanupMethodBody(this);

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public async Task DummyTestCleanupMethodAsync() => await TestCleanupMethodBodyAsync(this);

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void DummyTestMethod() => TestMethodBody(this);

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public Task DummyAsyncTestMethod() =>
            // We use this method to validate async TestInitialize, TestCleanup, TestMethod
            DummyAsyncTestMethodBody();

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void DummySimpleArgumentsMethod(string str1, string str2) => TestMethodBody(this);

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void DummyOptionalArgumentsMethod(string str1, string? str2 = null, string? str3 = null) => TestMethodBody(this);

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void DummyParamsArgumentMethod(int i, params string[] args) => TestMethodBody(this);
    }

    public class DummyTestClassWithParameterizedCtor
    {
        public DummyTestClassWithParameterizedCtor(int x)
        {
        }
    }

    public class DummyTestClassWithTestContextWithoutSetter
    {
        public TestContext TestContext { get; } = null!;
    }

    public class DummyTestClassWithDisposable : IDisposable
    {
        public static Action? DisposeMethodBody { get; set; }

        public static Action<DummyTestClassWithDisposable>? DummyTestCleanupMethodBody { get; set; }

        public void Dispose() => DisposeMethodBody!();

        public void DummyTestMethod()
        {
        }

        public void DummyTestCleanupMethod() => DummyTestCleanupMethodBody!(this);
    }

    #region Dummy implementation

    /// <summary>
    ///  Custom Expected exception attribute which overrides the Verify method.
    /// </summary>
    public class CustomExpectedExceptionAttribute : ExpectedExceptionBaseAttribute
    {
        public CustomExpectedExceptionAttribute(Type exceptionType, string noExceptionMessage)
            : base(noExceptionMessage) => ExceptionType = exceptionType;

        public bool IsVerifyInvoked { get; set; }

        public Type ExceptionType { get; private set; }

        protected internal override void Verify(Exception exception)
        {
            IsVerifyInvoked = true;
            if (exception is AssertInconclusiveException)
            {
                throw new AssertInconclusiveException();
            }
            else if (!exception.Message.Contains(NoExceptionMessage))
            {
                throw new InvalidOperationException("The exception message doesn't contain the string defined in the exception attribute");
            }
        }
    }

    /// <summary>
    ///  Custom Expected exception attribute which overrides the Verify method.
    /// </summary>
    public class DerivedCustomExpectedExceptionAttribute : CustomExpectedExceptionAttribute
    {
        public DerivedCustomExpectedExceptionAttribute(Type exceptionType, string noExceptionMessage)
            : base(exceptionType, noExceptionMessage) => ExceptionType = exceptionType;

        public new Type ExceptionType { get; private set; }

        public new bool IsVerifyInvoked { get; set; }

        protected internal override void Verify(Exception exception)
        {
            IsVerifyInvoked = true;
            if (exception is AssertInconclusiveException)
            {
                throw new AssertInconclusiveException();
            }
            else if (!exception.Message.Contains(NoExceptionMessage))
            {
                throw new InvalidOperationException("The exception message doesn't contain the string defined in the exception attribute");
            }
        }
    }

    #endregion

    public class DummyTestClassForExpectedException
    {
        private class MyExpectedException1Attribute : ExpectedExceptionBaseAttribute
        {
            protected internal override void Verify(Exception exception) => throw new NotImplementedException();
        }

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
        public class MyExpectedException2Attribute : ExpectedExceptionBaseAttribute
        {
            protected internal override void Verify(Exception exception) => throw new NotImplementedException();
        }

        [ExpectedException(typeof(Exception))]
        [MyExpectedException1]

        public void DummyTestMethod1()
        {
        }

        [MyExpectedException2]
        [MyExpectedException2]
        public void DummyTestMethod2()
        {
        }

        [ExpectedException(typeof(DivideByZeroException))]
        public void TestMethodWithExpectedException()
        {
        }

        public void TestMethodWithoutExpectedException()
        {
        }
    }

#if NET6_0_OR_GREATER
    public class DummyTestClassWithAsyncDisposable : IAsyncDisposable
    {
        public static Action? DisposeAsyncMethodBody { get; set; }

        public static Action<DummyTestClassWithDisposable>? DummyTestCleanupMethodBody { get; set; }

        public void DummyTestMethod()
        {
        }

        public ValueTask DisposeAsync()
        {
            DisposeAsyncMethodBody!();
            return ValueTask.CompletedTask;
        }
    }

    public class DummyTestClassWithAsyncDisposableAndDisposable : IAsyncDisposable, IDisposable
    {
        public static Action? DisposeMethodBody { get; set; }

        public static Action? DisposeAsyncMethodBody { get; set; }

        public static Action<DummyTestClassWithDisposable>? DummyTestCleanupMethodBody { get; set; }

        public void DummyTestMethod()
        {
        }

        public ValueTask DisposeAsync()
        {
            DisposeAsyncMethodBody!();
            return ValueTask.CompletedTask;
        }

        public void Dispose() => DisposeMethodBody!();
    }
#endif

}
#endregion
