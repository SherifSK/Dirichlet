﻿namespace Decompose.Numerics
{
    public interface IBitMatrix : IMatrix<bool>
    {
        void XorRows(int dst, int src);
        bool IsRowEmpty(int i);
        void Clear();
        void CopySubMatrix(IBitMatrix other, int row, int col);
    }
}