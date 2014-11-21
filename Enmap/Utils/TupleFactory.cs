using System;

namespace Enmap.Utils
{
    public class TupleFactory
    {
        public static object CreateTuple(object[] values)
        {
            if (values == null)
                throw new ArgumentNullException("values");

            switch (values.Length)
            {
                case 0:
                    throw new Exception("Values must contain at least one element");
                case 1:
                    return Tuple.Create(values[0]);
                case 2:
                    return Tuple.Create(values[0], values[1]);
                case 3:
                    return Tuple.Create(values[0], values[1], values[2]);
                case 4:
                    return Tuple.Create(values[0], values[1], values[2], values[3]);
                case 5:
                    return Tuple.Create(values[0], values[1], values[2], values[3], values[4]);
                case 6:
                    return Tuple.Create(values[0], values[1], values[2], values[3], values[4], values[5]);
                case 7:
                    return Tuple.Create(values[0], values[1], values[2], values[3], values[4], values[5], values[6]);
                case 8:
                    return Tuple.Create(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7]);
                default:
                    throw new Exception("Too many elements in values");
            }
        } 
    }
}