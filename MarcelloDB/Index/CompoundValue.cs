﻿using System;
using System.Linq;
using System.Collections.Generic;

namespace MarcelloDB.Index
{
    public abstract class CompoundValue : IComparable
    {
        internal abstract IEnumerable<object> GetValues();

        internal CompoundValue(){}

        #region IComparable implementation
        public int CompareTo(object objB)
        {
            var valuesA = GetValues();
            var valuesB = ((CompoundValue)objB).GetValues();
            return CompareValues(valuesA, valuesB);
        }
        #endregion

        public static CompoundValue<T1, T2> Build<T1, T2>(T1 value1, T2 value2)
        {
            return new CompoundValue<T1, T2>(value1, value2);
        }

        int CompareValues(IEnumerable<object> valuesA, IEnumerable<object> valuesB)
        {
            if (valuesA.Count() == 0)
                return 0;
            var firstA = valuesA.First();
            var firstB = valuesB.First();
            var compareResult = CompareValue(firstA, firstB);
            if (compareResult == 0)
            {
                return CompareValues(valuesA.Skip(1), valuesB.Skip(1));
            }
            return compareResult;

        }

        int CompareValue(object valA, object valB)
        {
            if (valA != null && valB == null)
            {
                return 1;
            }
            if (valA == null && valB != null)
            {
                return -1;
            }
            if (valA == null && valB == null)
            {
                return 0;
            }
            return ((IComparable)valA).CompareTo(valB);
        }
    }

    public class CompoundValue<T1, T2> : CompoundValue{

        public T1 P1 { get; set; }
        public T2 P2 { get; set; }

        internal CompoundValue(){
        }

        internal CompoundValue(T1 p1, T2 p2){
            this.P1 = p1;
            this.P2 = p2;
        }

        internal override IEnumerable<object> GetValues()
        {
            return new object[]{ this.P1, this.P2 };
        }

    }
}

