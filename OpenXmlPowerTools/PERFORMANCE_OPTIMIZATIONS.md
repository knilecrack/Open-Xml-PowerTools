# Performance Optimization Plan for WmlComparer

## Overview
This document outlines performance optimization opportunities identified in WmlComparer using modern .NET features like Span<T> and ReadOnlySpan<T>.

## Completed: Span-Based String Cleaning Method

### New Method Added to PtUtil.cs
```csharp
public static string RemoveChars(this string input, ReadOnlySpan<char> charsToRemove)
```

**Benefits:**
- Uses `Span<char>` for stack allocation (strings ≤ 256 chars)
- Eliminates intermediate string allocations from multiple `Replace()` calls
- Single-pass algorithm vs multiple passes
- Can handle any set of characters to remove in one operation

**Performance Impact:**
- **Before:** Multiple `Replace()` calls create N intermediate strings
- **After:** Single allocation for final result
- **Savings:** For 6 Replace calls on 100-char string: ~600 bytes → ~100 bytes

## Identified Hotspots in WmlComparer.cs

### 1. HIGH IMPACT: Hash Calculation with Replace Chains

**Location:** Lines 817, 900-904

**Current Code (Line 817):**
```csharp
var sha1Hash = PtUtils.XxHash3FoerUTF8String(
    acceptedRevisionElement.Value
        .Replace(" ", "")        // Regular space
        .Replace(" ", "")        // Non-breaking space (U+00A0)
        .Replace(" ", "")        // Em space (U+2003)
        .Replace("\n", "")
        .Replace(".", "")
        .Replace(",", "")
        .ToUpper());
```

**Problems:**
- 7 intermediate string allocations per hash calculation
- Hash calculation occurs in hot loop during comparison
- Called for every revision being compared

**Optimized Code:**
```csharp
// Unicode characters: regular space, non-breaking space, em space
ReadOnlySpan<char> charsToRemove = stackalloc char[] { ' ', '\u00A0', '\u2003', '\n', '.', ',' };
var cleanedText = acceptedRevisionElement.Value.RemoveChars(charsToRemove).ToUpper();
var sha1Hash = PtUtils.XxHash3FoerUTF8String(cleanedText);
```

**Expected Impact:**
- ~85% reduction in string allocations for this operation
- Faster execution due to single-pass processing
- Reduced GC pressure

**Current Location:** Lines 817, 900-904

### 2. MEDIUM IMPACT: String Concatenation in Hot Paths

**Location:** Line 906, 794, 871, 956, 960

**Current Pattern:**
```csharp
return ci.InsertBefore.ToString() + sha1Hash;
```

**Optimization:**
Use `string.Concat()` or string interpolation:
```csharp
return string.Concat(ci.InsertBefore.ToString(), sha1Hash);
// or
return $"{ci.InsertBefore}{sha1Hash}";
```

**Expected Impact:**
- Eliminates temporary string allocation from + operator
- Minor improvement (single allocation saved per operation)

### 3. LOW IMPACT: ToString() Calls in StringBuilder Appends

**Location:** Lines 553-563

**Current Pattern:**
```csharp
sbt.Append("Revised #" + (count++).ToString() + Environment.NewLine);
```

**Optimization:**
```csharp
sbt.Append("Revised #").Append(count++).Append(Environment.NewLine);
```

**Expected Impact:**
- Eliminates string concatenation before StringBuilder.Append
- Debugging code only, low overall impact

## Implementation Status

| Optimization | Status | Priority | Lines Affected |
|-------------|--------|----------|----------------|
| Span-based RemoveChars method | ✅ Complete | HIGH | PtUtil.cs:585-623 |
| Apply to Line 817 hash calc | ⏳ Pending | HIGH | WmlComparer.cs:817 |
| Apply to Lines 900-904 | ⏳ Pending | HIGH | WmlComparer.cs:900-904 |
| String.Concat for hash keys | ⏳ Pending | MEDIUM | WmlComparer.cs:906 |
| StringBuilder optimizations | ⏳ Pending | LOW | WmlComparer.cs:553-563 |

## Next Steps

1. **Apply RemoveChars to hash calculations** (Lines 817, 900-904)
   - Replace Replace() chains with single RemoveChars() call
   - Handle Unicode whitespace characters properly

2. **String concatenation improvements**
   - Replace + operator with string.Concat or interpolation
   - Focus on hot paths (GroupBy keys, hash calculations)

3. **Testing**
   - Run WmlComparer tests to verify correctness
   - Measure performance improvement (optional benchmark)
   - Ensure Unicode character handling is correct

## Potential Future Optimizations

### SearchValues<T> for Character Removal (.NET 8+)
```csharp
private static readonly SearchValues<char> CharsToRemove =
    SearchValues.Create(" \u00A0\u2003\n.,");

public static string RemoveCharsOptimized(this string input)
{
    return input.AsSpan().RemoveAny(CharsToRemove).ToString();
}
```

### ValueStringBuilder for Hot Paths
Consider using `ValueStringBuilder` (internal .NET type) for temporary string building without heap allocation.

### Parallel Processing
WmlComparer processes documents sequentially. Large documents could benefit from parallel processing of sections.

## Notes

- Unicode whitespace characters found in Replace chains:
  - `\u0020` - Regular space
  - `\u00A0` - Non-breaking space
  - `\u2003` - Em space

- Hash calculations use XxHash3 (fast, non-cryptographic)
- Most allocations occur during revision comparison and correlation
- StringConcatenate() already uses StringBuilder efficiently

## Benchmark Ideas

To measure impact:
1. Create test document with many revisions
2. Time WmlComparer.Compare() before/after optimization
3. Use BenchmarkDotNet for accurate measurements
4. Measure memory allocations using dotMemory

Expected improvement: 10-20% reduction in execution time for comparison operations, 20-30% reduction in memory allocations.
