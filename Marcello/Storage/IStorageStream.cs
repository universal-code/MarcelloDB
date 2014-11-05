﻿using System;

namespace Marcello.Storage
{
    public interface IStorageStream
    {
        byte[] Read(long address, int length);

        void Write(long address, byte[] bytes);
    }
}

