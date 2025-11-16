namespace MyTestLibrary.NestedTypes
{
    /// <summary>
    /// Top level public class with various nested types
    /// </summary>
    public class TopLevelClass
    {
        /// <summary>
        /// Public nested class
        /// </summary>
        public class PublicNestedClass
        {
            public string Name { get; set; }

            /// <summary>
            /// Deeply nested public class (3 levels)
            /// </summary>
            public class DeeplyNestedClass
            {
                public int Value { get; set; }

                /// <summary>
                /// Very deeply nested class (4 levels)
                /// </summary>
                public class VeryDeeplyNestedClass
                {
                    public void DoSomething() { }
                }
            }

            // Private nested class - should NOT be exported
            private class PrivateNestedClass
            {
                public string Hidden { get; set; }
            }
        }

        /// <summary>
        /// Public nested struct
        /// </summary>
        public struct PublicNestedStruct
        {
            public int X;
            public int Y;
        }

        /// <summary>
        /// Public nested enum
        /// </summary>
        public enum PublicNestedEnum
        {
            Option1,
            Option2,
            Option3
        }

        /// <summary>
        /// Public nested delegate
        /// </summary>
        public delegate void PublicNestedDelegate(string message);

        /// <summary>
        /// Public nested interface
        /// </summary>
        public interface IPublicNestedInterface
        {
            void Process();
        }

        /// <summary>
        /// Public nested static class
        /// </summary>
        public static class PublicNestedStaticClass
        {
            public static void StaticMethod() { }
            public static int StaticProperty { get; set; }
        }

        // Internal nested class - should NOT be exported
        internal class InternalNestedClass
        {
            public string InternalData { get; set; }
        }

        // Protected nested class - should be exported as nested protected
        protected class ProtectedNestedClass
        {
            public string ProtectedData { get; set; }
        }

        // Protected internal nested class
        protected internal class ProtectedInternalNestedClass
        {
            public string Data { get; set; }
        }
    }

    /// <summary>
    /// Static class with nested types
    /// </summary>
    public static class StaticTopLevelClass
    {
        /// <summary>
        /// Nested class in static class
        /// </summary>
        public class NestedInStatic
        {
            public void Method() { }
        }

        /// <summary>
        /// Nested static class in static class
        /// </summary>
        public static class StaticNestedInStatic
        {
            public static string Value { get; set; }
        }
    }

    /// <summary>
    /// Abstract class with nested types
    /// </summary>
    public abstract class AbstractTopLevelClass
    {
        /// <summary>
        /// Nested abstract class
        /// </summary>
        public abstract class NestedAbstractClass
        {
            public abstract void AbstractMethod();
        }

        /// <summary>
        /// Nested sealed class
        /// </summary>
        public sealed class NestedSealedClass
        {
            public string SealedProperty { get; set; }
        }
    }

    /// <summary>
    /// Generic class with nested types
    /// </summary>
    public class GenericTopLevelClass<T>
    {
        /// <summary>
        /// Nested generic class
        /// </summary>
        public class NestedGenericClass<U>
        {
            public T OuterValue { get; set; }
            public U InnerValue { get; set; }
        }

        /// <summary>
        /// Non-generic nested class in generic class
        /// </summary>
        public class NonGenericNested
        {
            public T Value { get; set; }
        }
    }

    // Internal top-level class - should NOT be exported at all
    internal class InternalTopLevelClass
    {
        public class PublicNestedInInternal
        {
            public string Value { get; set; }
        }
    }
}