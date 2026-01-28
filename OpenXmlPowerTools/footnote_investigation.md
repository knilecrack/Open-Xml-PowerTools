# Footnote/Endnote Comparison Issue Investigation

## Problem Statement
8 tests fail with the same pattern: footnotes or endnotes containing tables return 0 revisions when GetRevisions() is called, instead of the expected 5-7 revisions.

## Affected Tests
- WC-1660, WC-1670: Footnote-With-Table (expect 5, get 0)
- WC-1750, WC-1760: Endnote-With-Table (expect 6, get 0)
- WC-1710, WC-1720: Endnotes (expect 7, get 6) - off by one
- WC-1500: Long-Table (expect 2, get 10) - overcounting
- WC-1810: Image comparison sanity check failure

## Investigation Findings

### Code Flow Analysis

1. **Comparison Process** (CompareInternal):
   - Calls ProcessFootnoteEndnote to handle footnotes/endnotes
   - ProcessFootnoteEndnote only processes footnotes/endnotes whose REFERENCES changed
   - Creates comparison atoms for each footnote/endnote content
   - Applies revision tracking (ins/del elements)

2. **Revision Extraction** (GetRevisions):
   - Called on already-compared document
   - Extracts main document revisions
   - Calls GetFootnoteEndnoteRevisionList for footnotes/endnotes
   - GetFootnoteEndnoteRevisionList:
     - Gets all footnote/endnote elements from parts
     - For each one, calls CreateComparisonUnitAtomList
     - Groups atoms by correlation status
     - Filters out "Equal" status to get revisions

### Potential Issues Identified

#### 1. Singular vs Plural Mismatch (ATTEMPTED FIX - REVERTED)
**Location:** WmlComparer.cs lines 6994, 7003, 7028, 7040

**Problem:** In CreateComparisonUnitAtomListRecurse, when building ancestor chains:
- Line 6975 handles singular: `W.footnote`, `W.endnote`
- Lines 6994, 7003, 7028, 7040 check for plural: `W.footnotes`, `W.endnotes`

**Hypothesis:** This mismatch could cause incorrect ancestor chain building for elements within individual footnotes/endnotes.

**Attempted Fix:** Changed plural to singular in TakeWhile conditions
**Result:** Caused "Internal error" in ProcessFootnoteEndnote at line 2497
**Reason for Failure:** The change was too broad - some code legitimately needs to check for the plural container elements (footnotes/endnotes parts).

**Next Steps:** Need more targeted fix that only changes ancestor chain building in CreateComparisonUnitAtomList context.

#### 2. Missing Table Handling in Footnote Context
**Observation:** Tables (W.tbl) are handled via RecursionElements and AnnotateElementWithProps, but there may be special cases for tables in footnotes that aren't covered.

**Need to investigate:** Whether table comparison within footnotes requires different handling than in main document.

#### 3. ProcessFootnoteEndnote Filtering
**Location:** Lines 2358-2362

**Code:**
```csharp
var possiblyModifiedFootnotesEndNotes = listOfComparisonUnitAtoms
    .Where(cua =>
        cua.ContentElement.Name == W.footnoteReference ||
        cua.ContentElement.Name == W.endnoteReference)
    .ToList();
```

**Issue:** Only processes footnotes/endnotes whose REFERENCES appear in the comparison atoms. If a footnote's content changes but its reference doesn't move in the document, it might not be processed.

**Need to verify:** Are footnotes with table changes being skipped because their references aren't in listOfComparisonUnitAtoms?

## Testing Approach Needed

1. **Debug Output:** Enable s_False → s_True for relevant sections to see:
   - What atoms are being created for footnotes with tables
   - What revisions are being detected
   - Whether ProcessFootnoteEndnote is being called for these footnotes

2. **Minimal Reproduction:**
   - Create simple test documents with:
     - Footnote with just text (baseline)
     - Footnote with table
   - Compare them
   - Check what revisions are detected

3. **Targeted Fix:**
   - Once root cause is confirmed, apply minimal fix
   - May need to:
     - Fix ancestor chain building for footnote context
     - Ensure all footnotes are processed, not just ones with moved references
     - Add special handling for tables in footnotes

## Current Status
Investigation in progress. Initial fix attempt revealed the issue is more complex than a simple plural/singular mismatch. Requires deeper analysis of how footnote content changes are detected and processed.

## Recommendation
This issue requires significant debugging and may need:
1. Enabling debug output to trace actual execution
2. Creating minimal test cases
3. Potentially refactoring how footnote/endnote comparison works

Estimated complexity: HIGH
Estimated impact if fixed: 8 tests (40% of remaining failures)
