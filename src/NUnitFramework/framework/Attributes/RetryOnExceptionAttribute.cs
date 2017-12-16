// ***********************************************************************
// Copyright (c) 2017 Mitchell Maler
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// ***********************************************************************

using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;

namespace NUnit.Framework.Attributes
{
    /// <summary>
    /// <see cref="RetryOnExceptionAttribute" /> is used on a test method to specify that it should
    /// be ran again if it fails due to exception(s), up to a maximum number of times. If no exception type is passed
    /// it will rety on any exception.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class RetryOnExceptionAttribute : PropertyAttribute, IWrapSetUpTearDown
    {
        #region Constructors

        /// <inheritdoc />
        /// <summary>
        /// Construct a <see cref="T:ComponentValidation.Core.NunitExtras.RetryOnExceptionAttribute" />
        /// </summary>
        /// <param name="tryCount">The maximum number of times the test should be run if it fails</param>
        /// <param name="exceptionType">The type of exception the test should be run if it fails.</param>
        public RetryOnExceptionAttribute(int tryCount, Type exceptionType) : base(tryCount)
        {
            if (exceptionType != null)
            {
                _exceptionTypes = new List<Type>() {exceptionType};
            }

            _tryCount = tryCount;
        }

        /// <inheritdoc />
        /// <summary>
        /// Construct a <see cref="T:ComponentValidation.Core.NunitExtras.RetryOnExceptionAttribute" />
        /// </summary>
        /// <param name="tryCount">The maximum number of times the test should be run if it fails</param>
        /// <param name="exceptionTypes">Array of types of exceptions the test should be run if it fails.</param>
        public RetryOnExceptionAttribute(int tryCount, Type[] exceptionTypes) : base(tryCount)
        {    
            _exceptionTypes = new List<Type>();
            
            for (int i = 0; i < exceptionTypes.Length; i++)
            {
              _exceptionTypes.Add(exceptionTypes[i]);
            }

            _tryCount = tryCount;
        }

        /// <inheritdoc />
        /// <summary>
        /// Construct a <see cref="T:ComponentValidation.Core.NunitExtras.RetryOnExceptionAttribute" />
        /// </summary>
        /// <param name="tryCount">The maximum number of times the test should be run if it fails</param>
        public RetryOnExceptionAttribute(int tryCount) : base(tryCount)
        {
            _tryCount = tryCount;
        }

        #endregion

        #region Properties

        /// <summary>
        /// int value for how many times to retry a test on exception
        /// </summary>
        private readonly int _tryCount;

        /// <summary>
        /// String of the full name of an exception
        /// </summary>
        private readonly IList<Type> _exceptionTypes;

        #endregion

        #region IWrapSetUpTearDown Members

        /// <inheritdoc />
        /// <summary>
        /// Wrap a command and return the result.
        /// </summary>
        /// <param name="command">The command to be wrapped</param>
        /// <returns>The wrapped command</returns>
        public TestCommand Wrap(TestCommand command)
        {
            return new RetryOnExceptionCommand(command, _tryCount, _exceptionTypes);
        }

        #endregion

        #region Nested RetryOnExceptionCommand Class

        /// <inheritdoc />
        /// <summary>
        /// The test command for the <see cref="T:NUnit.Framework.RetryAttribute" />
        /// </summary>
        private class RetryOnExceptionCommand : DelegatingTestCommand
        {
            private readonly int _tryCount;

            private readonly IList<Type> _exceptionTypes;

            /// <inheritdoc />
            /// <summary>
            /// Initializes a new instance of the <see cref="!:RetryCommand" /> class.
            /// </summary>
            /// <param name="innerCommand">The inner command.</param>
            /// <param name="tryCount">The maximum number of repetitions</param>
            /// <param name="exceptionTypes">The list of exceptions to retry on.</param>
            public RetryOnExceptionCommand(TestCommand innerCommand, int tryCount, IList<Type> exceptionTypes)
                : base(innerCommand)
            {
                _exceptionTypes = exceptionTypes;
                _tryCount = tryCount;
            }

            /// <inheritdoc />
            /// <summary>
            /// Runs the test, saving a TestResult in the supplied TestExecutionContext.
            /// </summary>
            /// <param name="context">The context in which the test should run.</param>
            /// <returns>A TestResult</returns>
            public override TestResult Execute(TestExecutionContext context)
            {
                int count = _tryCount;

                // Checking if the passed in exception is an exception type
                // Will remove type if not an exception and will warn user.
                if (_exceptionTypes != null)
                {
                    for (int i = 0; i < _exceptionTypes.Count; i++)
                    {
                        // Check if types passed in is actually an exception type. 
                        if (!IsExceptionType(_exceptionTypes[i]))
                        {
                            // Warn user that their type is being ignored.
                            TypeIsNotAnExceptionWarning(_exceptionTypes[i]);
                            // Remove ignored exception from list.
                            _exceptionTypes.RemoveAt(i);
                        }
                    }
                }

                // Retry the test per the retry count
                while (count-- > 0)
                {
                    try
                    {
                        context.CurrentResult = innerCommand.Execute(context);
                    }
                    // Commands are supposed to catch exceptions, but some don't
                    // and we want to look at restructuring the API in the future.
                    catch (Exception ex)
                    {
                        if (context.CurrentResult == null) context.CurrentResult = context.CurrentTest.MakeTestResult();
                        context.CurrentResult.RecordException(ex);
                    }

                    // If test is not in an error state will break to fail test and not retry
                    if (context.CurrentResult.ResultState != ResultState.Error) break;

                    // Checking if no exception types were passed and will just retry on any exception)
                    if (_exceptionTypes == null) continue;
                    
                    bool expectedExceptionThrown = false;

                    // Loop through each passed in exception type to see if that caused the test error
                    for (int i = 0; i < _exceptionTypes.Count; i++)
                    {
                        if (context.CurrentResult.ExceptionType.Equals(_exceptionTypes[i].FullName))
                        {
                            expectedExceptionThrown = true;
                            break;
                        }
                    }

                    // If an exception was thrown but was not part of the list provided. Will end test run.
                    if (!expectedExceptionThrown)
                    {
                        break;
                    }
                }

                return context.CurrentResult;
            }
        }

        #endregion

        #region Helper Methods

        private static bool IsExceptionType(Type potentialDescendant)
        {
            return potentialDescendant.IsSubclassOf(typeof(Exception))
                   || potentialDescendant == typeof(Exception);
        }

        private static void TypeIsNotAnExceptionWarning(MemberInfo type)
        {
            TestContext.WriteLine(
                $"WARNING: The type {type.Name} specifed in the RetryOnExceptionAttribute is not an exception type and is being ignored.");
        }

        #endregion
    }
}
