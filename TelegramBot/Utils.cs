using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Sandwich
{
    public static class Utils
    {
        public static IEnumerable<T> GetEnumerableOfType<T>(params object[] constructorArgs) where T : class, IComparable<T>
        {
            List<T> objects = new List<T>();
            List<Type> types;
            try
            {
                types = Assembly.GetAssembly(typeof(T)).GetTypes().Where(myType => myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(typeof(T))).ToList();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(myType => myType != null && myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(typeof(T))).ToList();
            }

            foreach (Type type in types)
            {
                objects.Add((T)Activator.CreateInstance(type, constructorArgs));
            }
            objects.Sort();
            return objects;
        }
    }
}
