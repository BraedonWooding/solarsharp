using System;
using NUnit.Framework;
using SolarSharp.Interpreter.DataStructs;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    ///     Unit test suite for the FastStack data structure utilized by the virtual machine for stack-based operations.
    /// </summary>
    /// <remarks>
    ///     This test suite evaluates the following aspects of FastStack:
    ///     - Core functionality: Basic operations such as Push, Pop, and Peek
    ///     - Advanced features: Operations including Set, Expand, RemoveLast, and ClearUsed
    ///     - Robustness: Handling of edge cases, boundary conditions, and invalid operations
    ///     - Performance: Behaviors under scenarios such as large capacities and rapid operation sequences
    ///     - Compatibility: Type safety and support for various data types (value types, reference types, nulls, custom
    ///     structs)
    ///     The tests ensure data integrity, memory safety, and proper exception handling under different use cases.
    ///     Test separation: All tests are independent and use isolated stack instances for validation.
    /// </remarks>
    [TestFixture]
    [Category("DataStructureTest")]
    public class FastStackTests
    {
        /// <summary>
        ///     Tests the constructor of the FastStack
        ///     <T>
        ///         class to ensure that a stack is properly created
        ///         with the specified initial capacity.
        /// </summary>
        /// <remarks>
        ///     Verifies that the stack begins with zero count, the underlying storage array is non-null,
        ///     and the length of the storage array matches the specified capacity.
        /// </remarks>
        [Test]
        public void Constructor_CreatesStackWithSpecifiedCapacity()
        {
            var stack = new FastStack<int>(100);

            Assert.That(stack.Count, Is.EqualTo(0));
            Assert.That(stack.Storage, Is.Not.Null);
            Assert.That(stack.Storage.Length, Is.EqualTo(100));
        }

        /// <summary>
        ///     Adds an item to the stack and returns it.
        /// </summary>
        /// <typeparam name="T">The type of the item to be added to the stack.</typeparam>
        /// <param name="item">The item to push onto the stack.</param>
        /// <returns>The item that was pushed onto the stack.</returns>
        /// <remarks>
        ///     After the item is pushed, the stack's count is incremented, and the item becomes the new top of the stack.
        ///     The method ensures that the stack maintains the Last-In-First-Out (LIFO) order.
        ///     This method assumes the stack has sufficient capacity to accommodate the new item.
        /// </remarks>
        /// <exception cref="IndexOutOfRangeException">
        ///     Thrown if the stack's current capacity is insufficient to accommodate the new item.
        /// </exception>
        [Test]
        public void Push_AddsItemAndReturnsIt()
        {
            var stack = new FastStack<int>(10);

            var result = stack.Push(42);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(42));
                Assert.That(stack.Count, Is.EqualTo(1));
                Assert.That(stack.Peek(), Is.EqualTo(42));
            });
        }

        /// <summary>
        ///     Removes and returns the top item from the stack.
        /// </summary>
        /// <remarks>
        ///     This method ensures that the most recent item added to the stack is removed and returned,
        ///     maintaining the Last-In-First-Out (LIFO) behavior of the stack. After removal, the stack
        ///     count is decreased by one. The underlying data structure remains intact and can continue
        ///     to be used after the operation.
        /// </remarks>
        /// <exception cref="IndexOutOfRangeException">
        ///     Thrown if the method is called on an empty stack.
        /// </exception>
        /// <returns>
        ///     The item that was removed from the top of the stack.
        /// </returns>
        [Test]
        public void Pop_RemovesAndReturnsTopItem()
        {
            var stack = new FastStack<int>(10);
            stack.Push(1);
            stack.Push(2);
            stack.Push(3);

            var result = stack.Pop();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(3));
                Assert.That(stack.Count, Is.EqualTo(2));
                Assert.That(stack.Peek(), Is.EqualTo(2));
            });
        }

        /// <summary>
        ///     Verifies that the Push and Pop operations on the stack maintain Last-In-First-Out (LIFO) order.
        /// </summary>
        /// <remarks>
        ///     Ensures that items are returned in reverse order of their insertion when popped, and the stack
        ///     count becomes zero after all elements are removed.
        /// </remarks>
        /// <exception cref="AssertionException">
        ///     Thrown when the sequence of popped items does not match the reverse of the inserted sequence
        ///     or the stack count is not zero after all items are removed.
        /// </exception>
        [Test]
        public void Push_Pop_MaintainsLifoOrder()
        {
            var stack = new FastStack<string>(10);
            var items = new[] { "first", "second", "third" };

            foreach (var item in items)
                stack.Push(item);

            for (var i = items.Length - 1; i >= 0; i--) Assert.That(stack.Pop(), Is.EqualTo(items[i]));
            Assert.That(stack.Count, Is.EqualTo(0));
        }

        /// <summary>
        ///     Validates that the Peek method of the FastStack correctly returns the top element of the stack
        ///     without removing it from the stack. Ensures the stack's count remains unchanged after the operation
        ///     and that the top element can still be retrieved subsequently.
        /// </summary>
        /// <remarks>
        ///     This test checks the behavior of the Peek method on a non-empty stack. It asserts that the
        ///     returned element matches the top element, the count is not modified, and the top element
        ///     remains intact after the operation.
        /// </remarks>
        /// <exception cref="AssertionException">
        ///     Thrown if the Peek method does not return the expected element, modifies the stack count,
        ///     or fails to preserve the top element.
        /// </exception>
        [Test]
        public void Peek_ReturnsTopWithoutRemoving()
        {
            var stack = new FastStack<int>(10);
            stack.Push(1);
            stack.Push(2);

            var peeked = stack.Peek();
            var count = stack.Count;

            Assert.Multiple(() =>
            {
                Assert.That(peeked, Is.EqualTo(2));
                Assert.That(count, Is.EqualTo(2));
                Assert.That(stack.Peek(), Is.EqualTo(2)); // Still there
            });
        }

        /// <summary>
        ///     Verifies that the <see cref="FastStack{T}.Peek(int)" /> method returns the correct item
        ///     from the stack based on the specified offset from the top of the stack.
        /// </summary>
        /// <remarks>
        ///     This test checks stack behavior with multiple items. It confirms that:
        ///     - An offset of 0 retrieves the top item.
        ///     - Positive offsets retrieve items below the top item in the correct order.
        /// </remarks>
        /// <exception cref="AssertionException">
        ///     Thrown when the returned item does not match the expected value at the specified offset.
        /// </exception>
        [Test]
        public void Peek_WithOffset_ReturnsCorrectItem()
        {
            var stack = new FastStack<int>(10);
            stack.Push(10);
            stack.Push(20);
            stack.Push(30);
            stack.Push(40);

            Assert.Multiple(() =>
            {
                Assert.That(stack.Peek(), Is.EqualTo(40)); // Top
                Assert.That(stack.Peek(1), Is.EqualTo(30)); // One below top
                Assert.That(stack.Peek(2), Is.EqualTo(20)); // Two below top
                Assert.That(stack.Peek(3), Is.EqualTo(10)); // Bottom
            });
        }

        /// <summary>
        ///     Updates the value of an item at the specified offset within the stack.
        /// </summary>
        /// <param name="idxofs">
        ///     The zero-based offset from the top of the stack where the item is to be modified.
        ///     An offset of 0 refers to the top-most item, while increasing offsets refer to items further down the stack.
        /// </param>
        /// <param name="item">The new value to store at the specified offset.</param>
        /// <exception cref="IndexOutOfRangeException">
        ///     Thrown if the specified offset is outside the bounds of the current stack contents.
        /// </exception>
        /// <remarks>
        ///     This method does not affect the order of other items in the stack and operates directly
        ///     on the item at the specified offset. The stack capacity is not modified by this operation.
        /// </remarks>
        [Test]
        public void Set_ModifiesItemAtOffset()
        {
            var stack = new FastStack<string>(10);
            stack.Push("a");
            stack.Push("b");
            stack.Push("c");

            stack.Set(1, "modified");

            Assert.Multiple(() =>
            {
                Assert.That(stack.Peek(), Is.EqualTo("c"));
                Assert.That(stack.Peek(1), Is.EqualTo("modified"));
                Assert.That(stack.Peek(2), Is.EqualTo("a"));
            });
        }

        /// <summary>
        ///     Tests the behavior of the <c>Expand</c> method in the <c>FastStack</c> class
        ///     to verify that it increases the stack's count by the specified size
        ///     without adding non-default items to the expanded slots.
        /// </summary>
        /// <remarks>
        ///     The <c>Expand</c> method increases the logical count of the stack by adding uninitialized
        ///     space, which defaults to the type's default values. This test ensures the expanded slots
        ///     correctly contain default values while preserving the prior stack contents.
        /// </remarks>
        /// <exception cref="AssertionException">
        ///     Unsuccessful assertion if the count, default values, or prior stack contents
        ///     do not match the expected results after the expansion.
        /// </exception>
        [Test]
        public void Expand_IncreasesCountWithoutAddingItems()
        {
            var stack = new FastStack<int>(10);
            stack.Push(1);
            stack.Push(2);

            stack.Expand(3);

            Assert.Multiple(() =>
            {
                Assert.That(stack.Count, Is.EqualTo(5));
                Assert.That(stack.Peek(), Is.EqualTo(0)); // default(int)
                Assert.That(stack.Peek(1), Is.EqualTo(0)); // default(int)
                Assert.That(stack.Peek(2), Is.EqualTo(0)); // default(int)
                Assert.That(stack.Peek(3), Is.EqualTo(2)); // Original top
                Assert.That(stack.Peek(4), Is.EqualTo(1)); // Original bottom
            });
        }

        /// <summary>
        ///     Removes the specified number of items from the top of the stack. By default, one item is removed.
        /// </summary>
        /// <param name="cnt">The number of items to remove from the stack. If not specified, a single item is removed.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown if the number of items to remove exceeds the current count of the
        ///     stack.
        /// </exception>
        /// <remarks>
        ///     If the stack contains fewer elements than specified by <paramref name="cnt" />, an exception is thrown.
        ///     After removal, the stack's count is decreased, and the elements above the new top are cleared.
        /// </remarks>
        [Test]
        public void RemoveLast_RemovesSingleItem()
        {
            var stack = new FastStack<int>(10);
            stack.Push(1);
            stack.Push(2);
            stack.Push(3);

            stack.RemoveLast();

            Assert.That(stack.Count, Is.EqualTo(2));
            Assert.That(stack.Peek(), Is.EqualTo(2));
        }

        /// <summary>
        ///     Removes the specified number of items from the top of the stack.
        /// </summary>
        /// <param name="cnt">The number of items to remove from the stack. Default value is 1.</param>
        /// <remarks>
        ///     The method modifies the stack by decreasing its count and removing the most recently added items, adhering to the
        ///     LIFO (Last In, First Out) principle.
        ///     It ensures items are removed up to the specified count or until the stack is empty.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        ///     Thrown when attempting to remove more items than the current stack count.
        /// </exception>
        [Test]
        public void RemoveLast_RemovesMultipleItems()
        {
            var stack = new FastStack<int>(10);
            for (var i = 1; i <= 5; i++)
                stack.Push(i);

            stack.RemoveLast(3);

            Assert.That(stack.Count, Is.EqualTo(2));
            Assert.That(stack.Peek(), Is.EqualTo(2));
            Assert.That(stack.Peek(1), Is.EqualTo(1));
        }

        /// <summary>
        ///     Reduces the number of items in the stack to a specified count by removing
        ///     items from the top until the desired count is reached.
        /// </summary>
        /// <param name="p">
        ///     The target count to which the stack should be reduced.
        ///     This value must be less than or equal to the current count of items in the stack.
        /// </param>
        [Test]
        public void CropAtCount_RemovesItemsAboveCount()
        {
            var stack = new FastStack<int>(10);
            for (var i = 1; i <= 5; i++)
                stack.Push(i);

            stack.CropAtCount(3);

            Assert.That(stack.Count, Is.EqualTo(3));
            Assert.That(stack.Peek(), Is.EqualTo(3));
        }

        /// <summary>
        ///     Tests the ClearUsed method in the FastStack class to ensure that all items are removed from the stack and the
        ///     underlying storage is cleared.
        /// </summary>
        /// <remarks>
        ///     This test verifies the behavior of the ClearUsed method by populating the stack with several items, invoking
        ///     ClearUsed,
        ///     and confirming that the stack count is zero and the storage array has been reset to its default state.
        /// </remarks>
        [Test]
        public void ClearUsed_RemovesAllItems()
        {
            var stack = new FastStack<int>(10);
            for (var i = 1; i <= 5; i++)
                stack.Push(i);

            stack.ClearUsed();

            Assert.That(stack.Count, Is.EqualTo(0));
            // Verify storage is cleared
            for (var i = 0; i < 5; i++)
                Assert.That(stack.Storage[i], Is.EqualTo(0));
        }

        /// <summary>
        ///     Tests that the <c>Pop</c> method of <c>FastStack</c> throws an <c>IndexOutOfRangeException</c>
        ///     when called on an empty stack.
        /// </summary>
        /// <remarks>
        ///     Ensures that the stack implementation properly detects and handles attempts
        ///     to pop elements when no elements exist, by throwing the appropriate exception.
        /// </remarks>
        [Test]
        public void Pop_OnEmptyStack_ThrowsIndexOutOfRangeException()
        {
            var stack = new FastStack<int>(10);

            Assert.Throws<IndexOutOfRangeException>(() => stack.Pop());
        }

        /// <summary>
        ///     Verifies that calling the <c>Peek</c> method on an empty stack
        ///     throws an <c>IndexOutOfRangeException</c>.
        /// </summary>
        /// <remarks>
        ///     This test ensures the <c>FastStack</c> implementation properly handles
        ///     edge cases where the stack is empty, maintaining correct behavior
        ///     by throwing an appropriate exception when attempting to peek.
        /// </remarks>
        /// <exception cref="IndexOutOfRangeException">
        ///     Thrown when the <c>Peek</c> method is called on an empty stack.
        /// </exception>
        [Test]
        public void Peek_OnEmptyStack_ThrowsIndexOutOfRangeException()
        {
            var stack = new FastStack<int>(10);

            Assert.Throws<IndexOutOfRangeException>(() => stack.Peek());
        }

        /// <summary>
        ///     Tests that when trying to push an item onto the stack beyond its maximum capacity,
        ///     an IndexOutOfRangeException is thrown.
        /// </summary>
        /// <remarks>
        ///     This ensures that the `FastStack` correctly enforces its capacity constraints
        ///     and prevents overflow operations.
        ///     The test creates a stack with a predefined capacity, fills it to that capacity,
        ///     and then attempts to push an additional item, expecting an exception to be thrown.
        /// </remarks>
        /// <exception cref="IndexOutOfRangeException">
        ///     Thrown when the stack exceeds its defined maximum capacity during a push operation.
        /// </exception>
        [Test]
        public void Push_BeyondCapacity_ThrowsIndexOutOfRangeException()
        {
            var stack = new FastStack<int>(2);
            stack.Push(1);
            stack.Push(2);

            Assert.Throws<IndexOutOfRangeException>(() => stack.Push(3));
        }

        /// <summary>
        ///     Verifies that attempting to peek at an item in the stack with an offset
        ///     greater than or equal to the current number of items in the stack
        ///     throws an <see cref="IndexOutOfRangeException" />.
        /// </summary>
        /// <remarks>
        ///     This test ensures that the <c>Peek</c> method properly validates the offset
        ///     parameter and prevents access to elements outside the bounds of the stack.
        /// </remarks>
        /// <exception cref="IndexOutOfRangeException">
        ///     Thrown when the offset specified in the <c>Peek</c> method exceeds the
        ///     current bounds of the stack.
        /// </exception>
        [Test]
        public void Peek_WithOffsetBeyondCount_ThrowsIndexOutOfRangeException()
        {
            var stack = new FastStack<int>(10);
            stack.Push(1);
            stack.Push(2);

            Assert.Throws<IndexOutOfRangeException>(() => stack.Peek(2)); // Only 0 and 1 are valid
        }

        /// <summary>
        ///     Verifies that attempting to set an item at an offset beyond the current count of the stack
        ///     throws an <see cref="IndexOutOfRangeException" />.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     Thrown when the specified offset is greater than or equal to the count of items in the stack.
        /// </exception>
        [Test]
        public void Set_WithOffsetBeyondCount_ThrowsIndexOutOfRangeException()
        {
            var stack = new FastStack<int>(10);
            stack.Push(1);

            Assert.Throws<IndexOutOfRangeException>(() => stack.Set(1, 42));
        }

        /// <summary>
        ///     Verifies that expanding a stack beyond its internal capacity allows an increase in count,
        ///     but results in failure when attempting to access expanded space due to bounds violations.
        /// </summary>
        /// <remarks>
        ///     This test ensures that the <see cref="FastStack{T}.Expand" /> method doesn't validate bounds
        ///     during expansion, leading to potential access errors such as <see cref="IndexOutOfRangeException" />
        ///     when interacting with the expanded stack.
        /// </remarks>
        /// <exception cref="IndexOutOfRangeException">
        ///     Thrown when attempting to access elements in the expanded space beyond the stack's internal array bounds.
        /// </exception>
        [Test]
        public void Expand_BeyondCapacity_AllowsExpansionButFailsOnUse()
        {
            var stack = new FastStack<int>(5);
            stack.Push(1);
            stack.Push(2);

            // Expand doesn't check bounds
            stack.Expand(10); // This succeeds

            // But trying to use the space fails
            Assert.That(stack.Count, Is.EqualTo(12)); // 2 + 10
            Assert.Throws<IndexOutOfRangeException>(() => stack.Peek()); // Accessing beyond array bounds
        }

        /// <summary>
        ///     Tests the behavior of the <c>RemoveLast</c> method when attempting to remove
        ///     more items than are currently present in the stack.
        ///     Ensures that an exception of type <c>IndexOutOfRangeException</c> is thrown
        ///     in such scenarios.
        /// </summary>
        /// <remarks>
        ///     This test is designed to validate proper error handling in cases where the
        ///     removal count exceeds the current stack count. It confirms that the stack's
        ///     integrity is not compromised, and appropriate exceptions are raised for
        ///     invalid operations.
        /// </remarks>
        /// <exception cref="IndexOutOfRangeException">
        ///     Thrown when the number of items to remove exceeds the current count of
        ///     items in the stack.
        /// </exception>
        [Test]
        public void RemoveLast_MoreThanCount_ThrowsException()
        {
            var stack = new FastStack<int>(10);
            stack.Push(1);
            stack.Push(2);

            Assert.Throws<IndexOutOfRangeException>(() =>
                stack.RemoveLast(5)); // Zero() will fail with negative indices
        }

        /// <summary>
        ///     Validates that the <c>Pop</c> method removes the top item from the stack
        ///     and explicitly clears the corresponding storage slot by setting it to the default value for its type.
        /// </summary>
        /// <remarks>
        ///     This test ensures that when <c>Pop</c> is called, the stack not only returns and removes the top item
        ///     but also clears its reference in the underlying storage to prevent possible memory retention or unintended reuse.
        /// </remarks>
        [Test]
        public void Pop_ClearsRemovedItem()
        {
            var stack = new FastStack<string>(10);
            stack.Push("test");
            var initialStorage = stack.Storage[0];

            var popped = stack.Pop();

            Assert.That(popped, Is.EqualTo("test"));
            Assert.That(stack.Storage[0], Is.Null); // Cleared to default
        }

        /// <summary>
        ///     Verifies that the `RemoveLast` method of the `FastStack` class properly clears references
        ///     to removed items from the underlying storage array, leaving them null and ensuring no unintended
        ///     memory retention for reference types.
        /// </summary>
        /// <remarks>
        ///     This test ensures that after removing a given number of items (specified by the method parameter),
        ///     the corresponding slots in the stack's storage array become null while maintaining the integrity of
        ///     the remaining items in the stack.
        /// </remarks>
        /// <example>
        ///     The `RemoveLast` method is expected to nullify the references to removed objects in the storage for safety.
        ///     This behavior is particularly useful for avoiding memory issues when working with reference types.
        /// </example>
        /// <exception cref="Exception">
        ///     If the `RemoveLast` does not clear removed item slots to null, the test will fail to ensure memory retention
        ///     safety.
        /// </exception>
        [Test]
        public void RemoveLast_ClearsRemovedItems()
        {
            var stack = new FastStack<string>(10);
            stack.Push("a");
            stack.Push("b");
            stack.Push("c");

            stack.RemoveLast(2);

            Assert.That(stack.Count, Is.EqualTo(1));
            Assert.That(stack.Storage[1], Is.Null);
            Assert.That(stack.Storage[2], Is.Null);
            Assert.That(stack.Storage[0], Is.EqualTo("a")); // Still there
        }

        /// <summary>
        ///     Verifies that the underlying storage array of the stack is returned correctly.
        /// </summary>
        /// <remarks>
        ///     This test ensures that the <c>Storage</c> property of the <c>FastStack</c> class
        ///     correctly provides access to the internal array used to store the elements of the stack.
        ///     Validates that:
        ///     - The storage array is non-null.
        ///     - The storage array has the expected length, corresponding to the initial capacity specified during construction.
        ///     - The storage array contains the expected item at the correct position when elements are added to the stack.
        /// </remarks>
        [Test]
        public void Storage_ReturnsUnderlyingArray()
        {
            var stack = new FastStack<int>(10);
            stack.Push(42);

            var storage = stack.Storage;

            Assert.That(storage, Is.Not.Null);
            Assert.That(storage.Length, Is.EqualTo(10));
            Assert.That(storage[0], Is.EqualTo(42));
        }

        /// <summary>
        ///     Tests the functionality of the stack with value types.
        ///     Ensures that the stack correctly supports operations such as pushing,
        ///     popping, and peeking while maintaining the LIFO (Last In, First Out) order
        ///     when working with value types.
        /// </summary>
        [Test]
        public void Stack_WorksWithValueTypes()
        {
            var stack = new FastStack<int>(10);

            stack.Push(42);
            stack.Push(100);
            var result = stack.Pop();

            Assert.That(result, Is.EqualTo(100));
            Assert.That(stack.Peek(), Is.EqualTo(42));
        }

        /// <summary>
        ///     Validates the functionality of a <see cref="FastStack{T}" /> with reference types.
        ///     This includes ensuring proper behavior when pushing, popping, and peeking elements
        ///     in a last-in-first-out (LIFO) order using reference type data.
        /// </summary>
        /// <remarks>
        ///     Specifically tests if the stack correctly returns the most recently added
        ///     reference type item, maintains the order of remaining elements in the stack after
        ///     operations, and handles reference type-specific behavior.
        /// </remarks>
        [Test]
        public void Stack_WorksWithReferenceTypes()
        {
            var stack = new FastStack<string>(10);

            stack.Push("hello");
            stack.Push("world");
            var result = stack.Pop();

            Assert.That(result, Is.EqualTo("world"));
            Assert.That(stack.Peek(), Is.EqualTo("hello"));
        }

        /// <summary>
        ///     Tests the FastStack implementation for handling null references correctly.
        /// </summary>
        /// <remarks>
        ///     Validates that the stack can store and process `null` values in between other valid values properly.
        ///     The method checks that pushing `null` onto the stack does not interfere with its ability to maintain the
        ///     order and behavior expected of a stack, including Last-In-First-Out (LIFO) retrieval.
        /// </remarks>
        [Test]
        public void Stack_HandlesNullReferences()
        {
            var stack = new FastStack<string>(10);

            stack.Push(null);
            stack.Push("not null");
            stack.Push(null);

            Assert.That(stack.Pop(), Is.Null);
            Assert.That(stack.Pop(), Is.EqualTo("not null"));
            Assert.That(stack.Pop(), Is.Null);
        }

        /// <summary>
        ///     Verifies that the <see cref="FastStack{T}" /> can correctly handle operations when working with custom-defined
        ///     structs as stack elements.
        /// </summary>
        /// <remarks>
        ///     This method ensures that the stack behaves correctly with user-defined value types, such as pushing, popping,
        ///     and peeking elements in the correct order, as well as maintaining data integrity of the custom struct fields.
        /// </remarks>
        /// <exception cref="NUnit.Framework.AssertionException">
        ///     Thrown if the stack does not correctly handle custom structs or if operations do not behave as expected.
        /// </exception>
        [Test]
        public void Stack_WorksWithCustomStructs()
        {
            var stack = new FastStack<TestStruct>(10);
            var struct1 = new TestStruct { Value = 1, Name = "One" };
            var struct2 = new TestStruct { Value = 2, Name = "Two" };

            stack.Push(struct1);
            stack.Push(struct2);
            var result = stack.Pop();

            Assert.That(result.Value, Is.EqualTo(2));
            Assert.That(result.Name, Is.EqualTo("Two"));
            Assert.That(stack.Peek().Value, Is.EqualTo(1));
            Assert.That(stack.Peek().Name, Is.EqualTo("One"));
        }

        /// <summary>
        ///     Represents a simple structure with properties used as a value type in stack operations.
        /// </summary>
        /// <remarks>
        ///     This structure contains:
        ///     - An integer value to represent a numerical identifier or data point.
        ///     - A string to represent a descriptive name or label.
        ///     Usage scenarios:
        ///     - Testing stack operations with custom structs as elements.
        ///     - Verifying handling of value types in stack-based data structures.
        /// </remarks>
        private struct TestStruct
        {
            /// <summary>
            ///     Gets or sets the value associated with the instance.
            /// </summary>
            public int Value { get; set; }

            /// <summary>
            ///     Gets or sets the name associated with the object.
            /// </summary>
            public string Name { get; set; }
        }

        /// <summary>
        ///     Tests if the stack can handle a large pre-allocated capacity efficiently
        ///     while maintaining its intended behavior for operations such as pushing
        ///     and peeking elements.
        /// </summary>
        /// <remarks>
        ///     This test ensures that the stack is capable of handling a large underlying
        ///     storage array without performance degradation and verifies the integrity of
        ///     operations like pushing multiple items, checking the count, and peeking the
        ///     top element. It also confirms that the internal storage array is initialized
        ///     to the specified large capacity.
        /// </remarks>
        [Test]
        public void Stack_HandlesLargeCapacity()
        {
            // Use VM's actual stack size
            const int stackSize = 131072;
            var stack = new FastStack<int>(stackSize);

            // Push many items
            for (var i = 0; i < 1000; i++)
                stack.Push(i);

            Assert.That(stack.Count, Is.EqualTo(1000));
            Assert.That(stack.Peek(), Is.EqualTo(999));
            Assert.That(stack.Storage.Length, Is.EqualTo(stackSize));
        }

        /// <summary>
        ///     Verifies that the stack implementation can handle rapid push and pop operations efficiently without failure.
        ///     Ensures that the stack maintains the correct count of elements after repeated push and conditional pop cycles
        ///     across a large number of iterations. This test assesses the stability and consistency of the internal
        ///     stack mechanisms during high-frequency usage scenarios.
        /// </summary>
        /// <remarks>
        ///     This test is designed to simulate high-throughput operations where items are continuously
        ///     pushed onto the stack and popped off under conditions to verify that the stack remains
        ///     thread-safe, performant, and maintains proper state during intensive usage.
        /// </remarks>
        [Test]
        public void Stack_HandlesRapidPushPop()
        {
            var stack = new FastStack<int>(1000);
            const int iterations = 10000;

            // Rapid push/pop cycles
            var pushed = 0;
            var popped = 0;
            for (var i = 0; i < iterations && pushed - popped < 999; i++)
            {
                stack.Push(i);
                pushed++;
                if (i % 3 != 0 || stack.Count <= 0) continue;
                stack.Pop();
                popped++;
            }

            Assert.That(stack.Count, Is.EqualTo(pushed - popped));
        }

        /// <summary>
        ///     Represents a unit test that verifies the correct operation of a stack-based virtual machine pattern
        ///     during a simulated function call. This test ensures that the stack can correctly handle pushing a
        ///     function reference, multiple arguments, and an argument count, while maintaining the expected order
        ///     and structure of the stack.
        /// </summary>
        /// <remarks>
        ///     The test validates multiple aspects of the stack's behavior:
        ///     - Items are pushed onto the stack in the correct order (function reference, arguments, then argument count).
        ///     - The stack correctly maintains the count of its elements.
        ///     - Peek operations with offsets accurately reflect the order and values of the pushed items.
        ///     - The stack's Last-In-First-Out (LIFO) property is preserved as expected in a typical stack-based VM.
        /// </remarks>
        /// <example>
        ///     This test is specifically designed to simulate the behavior of a virtual machine handling a function
        ///     call, demonstrating a common stack manipulation pattern often used in interpreted languages or
        ///     low-level runtime environments.
        /// </example>
        [Test]
        public void Stack_SimulatesVmPattern_FunctionCall()
        {
            // Simulate VM calling a function with arguments
            var stack = new FastStack<int>(100);

            // Push function, arguments, then argument count (VM pattern)
            stack.Push(1000); // function reference
            stack.Push(10); // arg1
            stack.Push(20); // arg2
            stack.Push(30); // arg3
            stack.Push(3); // argument count

            // Verify stack state
            Assert.Multiple(() =>
            {
                Assert.That(stack.Count, Is.EqualTo(5));
                Assert.That(stack.Peek(), Is.EqualTo(3)); // arg count at top
                Assert.That(stack.Peek(1), Is.EqualTo(30)); // arg3
                Assert.That(stack.Peek(2), Is.EqualTo(20)); // arg2
                Assert.That(stack.Peek(3), Is.EqualTo(10)); // arg1
                Assert.That(stack.Peek(4), Is.EqualTo(1000)); // function
            });
        }

        /// <summary>
        ///     Tests a complex sequence of operations on the FastStack data structure.
        /// </summary>
        /// <remarks>
        ///     This method performs a series of stack operations including Push, Set, RemoveLast, Peek, and Expand.
        ///     It validates the stack's behavior under these operations, ensuring correct order, data integrity,
        ///     and adherence to LIFO principles. The stack's capacity and count are adjusted during the sequence,
        ///     and the final state of the stack is verified against expected values.
        /// </remarks>
        /// <exception cref="IndexOutOfRangeException">
        ///     Thrown if any operation exceeds the stack's capacity or operates outside its valid range.
        /// </exception>
        [Test]
        public void Stack_ComplexOperationSequence()
        {
            var stack = new FastStack<string>(20);

            stack.Push("base");
            stack.Push("layer1");
            stack.Push("layer2");
            stack.Set(1, "modified");
            stack.Push("layer3");
            stack.RemoveLast(2);
            stack.Push("new_top");
            var peeked = stack.Peek(1);
            stack.Expand(2);
            stack.Set(0, "expanded1");
            stack.Set(1, "expanded2");

            Assert.Multiple(() =>
            {
                Assert.That(stack.Count, Is.EqualTo(5));
                Assert.That(stack.Peek(), Is.EqualTo("expanded1"));
                Assert.That(stack.Peek(1), Is.EqualTo("expanded2"));
                Assert.That(stack.Peek(2), Is.EqualTo("new_top"));
                Assert.That(stack.Peek(3), Is.EqualTo("modified"));
                Assert.That(stack.Peek(4), Is.EqualTo("base"));
            });
        }
    }
}