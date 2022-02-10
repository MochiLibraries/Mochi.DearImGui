using System;

namespace Mochi.DearImGui
{
    public unsafe struct ImVector<T> : IDisposable
        where T : unmanaged
    {
        public int Size;
        public int Capacity;
        public T* Data;

        // inline ImVector()                                       { Size = Capacity = 0; Data = NULL; }
        // Default .NET behavior

        // inline ImVector(const ImVector<T>& src)                 { Size = Capacity = 0; Data = NULL; operator=(src); }
        // inline ImVector<T>& operator=(const ImVector<T>& src)   { clear(); resize(src.Size); memcpy(Data, src.Data, (size_t)Size * sizeof(T)); return *this; }
        public ImVector(ImVector<T> src)
        {
            this = default;
            resize(src.Size);
            src.AsSpan().CopyTo(this.AsSpan());
        }

        // inline ~ImVector()                                      { if (Data) IM_FREE(Data); }
        public void Dispose()
        {
            if (Data != null)
            { ImGui.MemFree(Data); }
        }

        // inline bool         empty() const                       { return Size == 0; }
        public readonly bool empty()
            => Size == 0;

        // inline int          size() const                        { return Size; }
        public readonly int size()
            => Size;

        // inline int          size_in_bytes() const               { return Size * (int)sizeof(T); }
        public readonly int size_in_bytes()
            => Size * sizeof(T);

        // inline int          max_size() const                    { return 0x7FFFFFFF / (int)sizeof(T); }
        public readonly int max_size()
            => 0x7FFFFFFF / sizeof(T);

        // inline int          capacity() const                    { return Capacity; }
        public readonly int capacity()
            => Capacity;

        // inline T&           operator[](int i)                   { IM_ASSERT(i < Size); return Data[i]; }
        // inline const T&     operator[](int i) const             { IM_ASSERT(i < Size); return Data[i]; }
        public readonly ref T this[int i]
        {
            get
            {
                if ((uint)i > (uint)Size)
                { throw new IndexOutOfRangeException(); }

                return ref Data[i];
            }
        }

        // inline void         clear()                             { if (Data) { Size = Capacity = 0; IM_FREE(Data); Data = NULL; } }
        public void clear()
        {
            if (Data != null)
            {
                Size = Capacity = 0;
                ImGui.MemFree(Data);
                Data = null;
            }
        }

        // inline T*           begin()                             { return Data; }
        // inline const T*     begin() const                       { return Data; }
        public readonly T* begin()
            => Data;

        // inline T*           end()                               { return Data + Size; }
        // inline const T*     end() const                         { return Data + Size; }
        public readonly T* end()
            => Data + Size;

        // inline T&           front()                             { IM_ASSERT(Size > 0); return Data[0]; }
        // inline const T&     front() const                       { IM_ASSERT(Size > 0); return Data[0]; }
        public readonly ref T front()
            => ref this[0];

        // inline T&           back()                              { IM_ASSERT(Size > 0); return Data[Size - 1]; }
        // inline const T&     back() const                        { IM_ASSERT(Size > 0); return Data[Size - 1]; }
        public readonly ref T back()
            => ref this[Size - 1];

        // inline void         swap(ImVector<T>& rhs)              { int rhs_size = rhs.Size; rhs.Size = Size; Size = rhs_size; int rhs_cap = rhs.Capacity; rhs.Capacity = Capacity; Capacity = rhs_cap; T* rhs_data = rhs.Data; rhs.Data = Data; Data = rhs_data; }
        public void swap(ref ImVector<T> rhs)
        {
            int rhs_size = rhs.Size;
            rhs.Size = Size;
            Size = rhs_size;
            int rhs_cap = rhs.Capacity;
            rhs.Capacity = Capacity;
            Capacity = rhs_cap;
            T* rhs_data = rhs.Data;
            rhs.Data = Data;
            Data = rhs_data;
        }

        // inline int          _grow_capacity(int sz) const        { int new_capacity = Capacity ? (Capacity + Capacity / 2) : 8; return new_capacity > sz ? new_capacity : sz; }
        public readonly int _grow_capacity(int sz)
        {
            int new_capacity = Capacity != 0 ? (Capacity + Capacity / 2) : 8;
            return new_capacity > sz ? new_capacity : sz;
        }

        // inline void         resize(int new_size)                { if (new_size > Capacity) reserve(_grow_capacity(new_size)); Size = new_size; }
        public void resize(int new_size)
        {
            if (new_size > Capacity)
            { reserve(_grow_capacity(new_size)); }

            Size = new_size;
        }

        // inline void         resize(int new_size, const T& v)    { if (new_size > Capacity) reserve(_grow_capacity(new_size)); if (new_size > Size) for (int n = Size; n < new_size; n++) memcpy(&Data[n], &v, sizeof(v)); Size = new_size; }
        public void resize(int new_size, in T v)
        {
            if (new_size > Capacity)
            { reserve(_grow_capacity(new_size)); }

            if (new_size > Size)
            {
                for (int n = Size; n < new_size; n++)
                { Data[n] = v; }
            }

            Size = new_size;
        }

        // inline void         shrink(int new_size)                { IM_ASSERT(new_size <= Size); Size = new_size; } // Resize a vector to a smaller size, guaranteed not to cause a reallocation
        public void shrink(int new_size)
        {
            if (new_size > Size)
            { throw new ArgumentOutOfRangeException(nameof(new_size)); }

            Size = new_size;
        }

        // inline void         reserve(int new_capacity)           { if (new_capacity <= Capacity) return; T* new_data = (T*)IM_ALLOC((size_t)new_capacity * sizeof(T)); if (Data) { memcpy(new_data, Data, (size_t)Size * sizeof(T)); IM_FREE(Data); } Data = new_data; Capacity = new_capacity; }
        public void reserve(int new_capacity)
        {
            if (new_capacity <= Capacity)
            { return; }

            T* new_data = (T*)ImGui.MemAlloc((nuint)(new_capacity * sizeof(T)));

            if (Data != null)
            {
                AsSpan().CopyTo(new Span<T>(new_data, Size));
                ImGui.MemFree(Data);
            }

            Data = new_data;
            Capacity = new_capacity;
        }

        // // NB: It is illegal to call push_back/push_front/insert with a reference pointing inside the ImVector data itself! e.g. v.push_back(v[10]) is forbidden.
        // inline void         push_back(const T& v)               { if (Size == Capacity) reserve(_grow_capacity(Size + 1)); memcpy(&Data[Size], &v, sizeof(v)); Size++; }
        public void push_back(in T v)
        {
            if (Size == Capacity)
            { reserve(_grow_capacity(Size + 1)); }

            Data[Size] = v;
            Size++;
        }

        // inline void         pop_back()                          { IM_ASSERT(Size > 0); Size--; }
        public void pop_back()
        {
            if (Size <= 0)
            { throw new InvalidOperationException(); }

            Size--;
        }

        // inline void         push_front(const T& v)              { if (Size == 0) push_back(v); else insert(Data, v); }
        public void push_front(in T v)
        {
            if (Size == 0)
            { push_back(v); }
            else
            { insert(Data, v); }
        }

        // inline T*           erase(const T* it)                  { IM_ASSERT(it >= Data && it < Data + Size); const ptrdiff_t off = it - Data; memmove(Data + off, Data + off + 1, ((size_t)Size - (size_t)off - 1) * sizeof(T)); Size--; return Data + off; }
        public T* erase(T* it)
        {
            if (it < Data || it >= (Data + Size))
            { throw new ArgumentOutOfRangeException(nameof(it)); }

            int off = (int)(it - Data);
            Span<T> span = AsSpan();
            span.Slice(off + 1).CopyTo(span.Slice(off));
            Size--;

            return Data + off;
        }

        // inline T*           erase(const T* it, const T* it_last){ IM_ASSERT(it >= Data && it < Data + Size && it_last > it && it_last <= Data + Size); const ptrdiff_t count = it_last - it; const ptrdiff_t off = it - Data; memmove(Data + off, Data + off + count, ((size_t)Size - (size_t)off - count) * sizeof(T)); Size -= (int)count; return Data + off; }
        public T* erase(T* it, T* it_last)
        {
            if (it < Data || it >= (Data + Size))
            { throw new ArgumentOutOfRangeException(nameof(it)); }

            if (it_last < Data || it_last >= (Data + Size))
            { throw new ArgumentOutOfRangeException(nameof(it_last)); }

            int count = (int)(it_last - it);
            int off = (int)(it - Data);
            Span<T> span = AsSpan();
            span.Slice(off + count).CopyTo(span.Slice(off));
            Size -= count;

            return Data + off;
        }

        // inline T*           erase_unsorted(const T* it)         { IM_ASSERT(it >= Data && it < Data + Size);  const ptrdiff_t off = it - Data; if (it < Data + Size - 1) memcpy(Data + off, Data + Size - 1, sizeof(T)); Size--; return Data + off; }
        public T* erase_unsorted(T* it)
        {
            if (it < Data || it >= (Data + Size))
            { throw new ArgumentOutOfRangeException(nameof(it)); }

            int off = (int)(it - Data);

            if (it < Data + Size - 1)
            {
                Span<T> span = AsSpan();
                span.Slice(Size - 1).CopyTo(span.Slice(off));
            }

            Size--;

            return Data + off;
        }
        // inline T*           insert(const T* it, const T& v)     { IM_ASSERT(it >= Data && it <= Data + Size); const ptrdiff_t off = it - Data; if (Size == Capacity) reserve(_grow_capacity(Size + 1)); if (off < (int)Size) memmove(Data + off + 1, Data + off, ((size_t)Size - (size_t)off) * sizeof(T)); memcpy(&Data[off], &v, sizeof(v)); Size++; return Data + off; }
        public T* insert(T* it, in T v)
        {
            if (it < Data || it >= (Data + Size))
            { throw new ArgumentOutOfRangeException(nameof(it)); }

            int off = (int)(it - Data);

            if (Size == Capacity)
            { reserve(_grow_capacity(Size + 1)); }

            if (off < Size)
            {
                Span<T> span = AsSpan();
                span.Slice(off).CopyTo(span.Slice(off + 1));
            }

            Data[off] = v;
            Size++;

            return Data + off;
        }

        // inline bool         contains(const T& v) const          { const T* data = Data;  const T* data_end = Data + Size; while (data < data_end) if (*data++ == v) return true; return false; }
        public readonly bool contains(in T v)
        {
            T* data = Data;
            T* data_end = Data + Size;

            //PERF: This method cannot be implemented in C# in a performance-friendly manner unless Biohazrd emits structs as IEquatable<T>
            while (data < data_end)
            {
                if ((*data++).Equals(v))
                { return true; }
            }

            return false;
        }

        // inline T*           find(const T& v)                    { T* data = Data;  const T* data_end = Data + Size; while (data < data_end) if (*data == v) break; else ++data; return data; }
        // inline const T*     find(const T& v) const              { const T* data = Data;  const T* data_end = Data + Size; while (data < data_end) if (*data == v) break; else ++data; return data; }
        public readonly T* find(in T v)
        {
            T* data = Data;
            T* data_end = Data + Size;

            while (data < data_end)
            {
                if ((*data).Equals(v))
                { break; }
                else
                { ++data; }
            }

            return data;
        }

        // inline bool         find_erase(const T& v)              { const T* it = find(v); if (it < Data + Size) { erase(it); return true; } return false; }
        public bool find_erase(in T v)
        {
            T* it = find(v);
            if (it < Data + Size)
            {
                erase(it);
                return true;
            }

            return false;
        }

        // inline bool         find_erase_unsorted(const T& v)     { const T* it = find(v); if (it < Data + Size) { erase_unsorted(it); return true; } return false; }
        public bool find_erase_unsorted(in T v)
        {
            T* it = find(v);
            if (it < Data + Size)
            {
                erase_unsorted(it);
                return true;
            }

            return false;
        }

        // inline int          index_from_ptr(const T* it) const   { IM_ASSERT(it >= Data && it < Data + Size); const ptrdiff_t off = it - Data; return (int)off; }
        public readonly int idex_from_ptr(T* it)
        {
            if (it < Data || it >= (Data + Size))
            { throw new ArgumentOutOfRangeException(nameof(it)); }

            int off = (int)(it - Data);
            return off;
        }

        // Mochi.DearImGui-specific methods
        public readonly Span<T> AsSpan()
            => new Span<T>(Data, Size);
    }
}
