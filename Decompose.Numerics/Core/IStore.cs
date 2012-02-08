﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Decompose.Numerics
{
    public interface IStore<T>
    {
        T Allocate();
        void Release(T item);
    }
}
