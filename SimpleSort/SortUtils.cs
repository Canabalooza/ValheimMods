using System;
using System.Collections.Generic;

namespace SimpleSort;

internal static class SortUtils
{
    internal static void SortByName(List<ItemDrop.ItemData> items, bool ascending)
    {
        items.Sort((a, b) =>
        {
            if (a.m_shared.m_name == b.m_shared.m_name)
            {
                return CompareInts(a.m_stack, b.m_stack, false);
            }

            return CompareStrings(
                Localization.instance.Localize(a.m_shared.m_name),
                Localization.instance.Localize(b.m_shared.m_name),
                ascending);
        });
    }

    internal static void SortByWeight(List<ItemDrop.ItemData> items, bool ascending)
    {
        items.Sort((a, b) =>
        {
            if (Math.Abs(a.m_shared.m_weight - b.m_shared.m_weight) < float.Epsilon)
            {
                if (a.m_shared.m_name == b.m_shared.m_name)
                {
                    return CompareInts(a.m_stack, b.m_stack, false);
                }

                return CompareStrings(
                    Localization.instance.Localize(a.m_shared.m_name),
                    Localization.instance.Localize(b.m_shared.m_name),
                    true);
            }

            return CompareFloats(a.m_shared.m_weight, b.m_shared.m_weight, ascending);
        });
    }

    internal static void SortByValue(List<ItemDrop.ItemData> items, bool ascending)
    {
        items.Sort((a, b) =>
        {
            if (a.m_shared.m_value == b.m_shared.m_value)
            {
                if (a.m_shared.m_name == b.m_shared.m_name)
                {
                    return CompareInts(a.m_stack, b.m_stack, false);
                }

                return CompareStrings(
                    Localization.instance.Localize(a.m_shared.m_name),
                    Localization.instance.Localize(b.m_shared.m_name),
                    true);
            }

            return CompareInts(a.m_shared.m_value, b.m_shared.m_value, ascending);
        });
    }

    private static int CompareStrings(string a, string b, bool ascending)
    {
        return ascending
            ? string.Compare(a, b, StringComparison.CurrentCulture)
            : string.Compare(b, a, StringComparison.CurrentCulture);
    }

    private static int CompareFloats(float a, float b, bool ascending)
    {
        return ascending ? a.CompareTo(b) : b.CompareTo(a);
    }

    private static int CompareInts(int a, int b, bool ascending)
    {
        return ascending ? a.CompareTo(b) : b.CompareTo(a);
    }
}
