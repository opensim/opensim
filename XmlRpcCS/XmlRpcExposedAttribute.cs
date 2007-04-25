namespace Nwc.XmlRpc
{
    using System;
    using System.Reflection;

    /// <summary>
    /// Simple tagging attribute to indicate participation is XML-RPC exposure.
    /// </summary>
    /// <remarks>
    /// If present at the class level it indicates that this class does explicitly 
    /// expose methods. If present at the method level it denotes that the method
    /// is exposed.
    /// </remarks>
    [AttributeUsage(
          AttributeTargets.Class | AttributeTargets.Method,
          AllowMultiple = false,
          Inherited = true
      )]
    public class XmlRpcExposedAttribute : Attribute
    {
        /// <summary>Check if <paramref>obj</paramref> is an object utilizing the XML-RPC exposed Attribute.</summary>
        /// <param name="obj"><c>Object</c> of a class or method to check for attribute.</param>
        /// <returns><c>Boolean</c> true if attribute present.</returns>
        public static Boolean ExposedObject(Object obj)
        {
            return IsExposed(obj.GetType());
        }

        /// <summary>Check if <paramref>obj</paramref>.<paramref>methodName</paramref> is an XML-RPC exposed method.</summary>
        /// <remarks>A method is considered to be exposed if it exists and, either, the object does not use the XmlRpcExposed attribute,
        /// or the object does use the XmlRpcExposed attribute and the method has the XmlRpcExposed attribute as well.</remarks>
        /// <returns><c>Boolean</c> true if the  method is exposed.</returns>
        public static Boolean ExposedMethod(Object obj, String methodName)
        {
            Type type = obj.GetType();
            MethodInfo method = type.GetMethod(methodName);

            if (method == null)
                throw new MissingMethodException("Method " + methodName + " not found.");

            if (!IsExposed(type))
                return true;

            return IsExposed(method);
        }

        /// <summary>Check if <paramref>mi</paramref> is XML-RPC exposed.</summary>
        /// <param name="mi"><c>MemberInfo</c> of a class or method to check for attribute.</param>
        /// <returns><c>Boolean</c> true if attribute present.</returns>
        public static Boolean IsExposed(MemberInfo mi)
        {
            foreach (Attribute attr in mi.GetCustomAttributes(true))
            {
                if (attr is XmlRpcExposedAttribute)
                    return true;
            }
            return false;
        }
    }
}
