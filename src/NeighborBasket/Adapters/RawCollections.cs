using System;
using Il2CppInterop.Runtime;

namespace NeighborBasket.Adapters;

/// <summary>
/// Reads a game Dictionary&lt;long, reference&gt; straight from its memory, so walking one makes no interop object per
/// entry. The layout is checked against the game's own type data once: where the entry array and the entry count sit,
/// and that an entry is a hash code, a link, a long key and a reference, 24 bytes in all. Each walk checks the object's
/// class and the array's class and length before reading, and reports false for anything it does not recognise, so
/// the caller can fall back to the interop path. Pointers are only good within the frame they were read in.
/// </summary>
internal sealed unsafe class RawDictionary
{
    private const int EntrySize = 24;
    private const int KeyAt = 8;
    private const int ValueAt = 16;

    private readonly IntPtr _class;
    private readonly int _entriesOffset;
    private readonly int _countOffset;
    private IntPtr _entriesClass;

    private RawDictionary(IntPtr dictionaryClass, int entriesOffset, int countOffset)
    {
        _class = dictionaryClass;
        _entriesOffset = entriesOffset;
        _countOffset = countOffset;
    }

    /// <summary>The reader for this dictionary class, or null when its fields are not the ones expected.</summary>
    public static RawDictionary? Learn(IntPtr dictionaryClass)
    {
        if (dictionaryClass == IntPtr.Zero) return null;

        var entries = FieldOffset(dictionaryClass, "_entries");
        var count = FieldOffset(dictionaryClass, "_count");

        return entries > 0 && count > 0 ? new RawDictionary(dictionaryClass, entries, count) : null;
    }

    /// <summary>
    /// The entry array and how many of its slots were ever used; false when the object is not this dictionary class or
    /// its array is not laid out as expected. An empty dictionary may have no array at all: zero slots.
    /// </summary>
    public bool TryOpen(IntPtr dictionary, out IntPtr entries, out int used)
    {
        entries = IntPtr.Zero;
        used = 0;

        if (dictionary == IntPtr.Zero || *(IntPtr*)dictionary != _class) return false;

        var array = *(IntPtr*)(dictionary + _entriesOffset);
        var count = *(int*)(dictionary + _countOffset);

        if (array == IntPtr.Zero) return count == 0;

        var arrayClass = *(IntPtr*)array;

        if (arrayClass != _entriesClass)
        {
            if (_entriesClass != IntPtr.Zero || !IsEntryArray(arrayClass)) return false;

            _entriesClass = arrayClass;
        }

        var length = *(long*)(array + 3 * IntPtr.Size);

        if (count < 0 || count > length) return false;

        entries = array + 4 * IntPtr.Size;
        used = count;

        return true;
    }

    /// <summary>A removed entry keeps its slot with a hash code of -1 and no value.</summary>
    public static bool IsLive(IntPtr entries, int index) => *(int*)(entries + index * EntrySize) >= 0;

    public static long Key(IntPtr entries, int index) => *(long*)(entries + index * EntrySize + KeyAt);

    public static IntPtr Value(IntPtr entries, int index) => *(IntPtr*)(entries + index * EntrySize + ValueAt);

    /// <summary>The entry is { int hashCode; int next; long key; reference value }, as the game's own type data says.</summary>
    private static bool IsEntryArray(IntPtr arrayClass)
    {
        if (IL2CPP.il2cpp_array_element_size(arrayClass) != EntrySize) return false;

        var entryClass = IL2CPP.il2cpp_class_get_element_class(arrayClass);

        if (entryClass == IntPtr.Zero) return false;

        var hashCode = FieldOffset(entryClass, "hashCode");

        // A struct's field offsets count the object header it would have when boxed.
        if (hashCode != 0 && hashCode != 2 * IntPtr.Size) return false;

        return FieldOffset(entryClass, "key") == hashCode + KeyAt && FieldOffset(entryClass, "value") == hashCode + ValueAt;
    }

    /// <summary>The field's offset in the object, or -1 when the class has no such field.</summary>
    public static int FieldOffset(IntPtr klass, string name)
    {
        var field = IL2CPP.GetIl2CppField(klass, name);

        return field == IntPtr.Zero ? -1 : (int)IL2CPP.il2cpp_field_get_offset(field);
    }
}

/// <summary>
/// Reads a game List&lt;reference&gt; straight from its memory: its item array and its size, at the offsets the game's
/// type data gives, checked against the array's length before any item is read.
/// </summary>
internal sealed unsafe class RawList
{
    private readonly IntPtr _class;
    private readonly int _itemsOffset;
    private readonly int _sizeOffset;
    private IntPtr _itemsClass;

    private RawList(IntPtr listClass, int itemsOffset, int sizeOffset)
    {
        _class = listClass;
        _itemsOffset = itemsOffset;
        _sizeOffset = sizeOffset;
    }

    /// <summary>The reader for this list class, or null when its fields are not the ones expected.</summary>
    public static RawList? Learn(IntPtr listClass)
    {
        if (listClass == IntPtr.Zero) return null;

        var items = RawDictionary.FieldOffset(listClass, "_items");
        var size = RawDictionary.FieldOffset(listClass, "_size");

        return items > 0 && size > 0 ? new RawList(listClass, items, size) : null;
    }

    /// <summary>The first item and the item count; false when the object is not this list class or its size is out of range.</summary>
    public bool TryOpen(IntPtr list, out IntPtr items, out int count)
    {
        items = IntPtr.Zero;
        count = 0;

        if (list == IntPtr.Zero || *(IntPtr*)list != _class) return false;

        var array = *(IntPtr*)(list + _itemsOffset);
        var size = *(int*)(list + _sizeOffset);

        if (array == IntPtr.Zero) return size == 0;

        var arrayClass = *(IntPtr*)array;

        if (arrayClass != _itemsClass)
        {
            if (IL2CPP.il2cpp_array_element_size(arrayClass) != IntPtr.Size) return false;

            _itemsClass = arrayClass;
        }

        var length = *(long*)(array + 3 * IntPtr.Size);

        if (size < 0 || size > length) return false;

        items = array + 4 * IntPtr.Size;
        count = size;

        return true;
    }

    public static IntPtr Item(IntPtr items, int index) => *(IntPtr*)(items + index * IntPtr.Size);
}
