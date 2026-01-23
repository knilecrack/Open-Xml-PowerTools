# Analysis of Remaining 20 Test Failures

## Summary by Category

### 1. WmlComparer Footnote/Endnote Issues (8 failures) - POTENTIALLY FIXABLE
**Pattern:** Tests involving footnotes/endnotes with tables return 0 revisions instead of expected count

**Failing Tests:**
- WC-1660: Footnote-With-Table (expected 5, actual 0)
- WC-1670: Footnote-With-Table reversed (expected 5, actual 0)
- WC-1750: Endnote-With-Table (expected 6, actual 0)
- WC-1760: Endnote-With-Table reversed (expected 6, actual 0)

**Hypothesis:** The comparison algorithm may not be properly detecting changes within footnotes/endnotes that contain tables. This could be a missing code path or a logic issue in how footnote/endnote content is compared.

**Investigation Path:**
- Check WmlComparer footnote/endnote comparison logic
- Verify table comparison within footnotes/endnotes
- Look for any special handling of nested structures

### 2. WmlComparer Image Comparison Issues (2 failures) - PARTIALLY INVESTIGATED
**Failing Tests:**
- WC-1240: Image comparison throws "PackageRelationship with specified ID does not exist"
- WC-1810: Image comparison fails sanity check #2

**Error Details:**
- WC-1240: Exception in `MoveRelatedPartsToDestination` at WmlComparer.cs:4753
- WC-1810: Sanity check failure (comparison produces unexpected revisions)

**Hypothesis:** Image handling in comparisons may have relationship tracking issues. The first error suggests we're trying to access a relationship that doesn't exist, possibly cleaned up or not copied properly.

### 3. WmlComparer Endnote Revision Count Off-By-One (2 failures)
**Failing Tests:**
- WC-1720: Endnotes (expected 7, actual 6)
- WC-1710: Endnotes (expected 7, actual 6)

**Hypothesis:** Endnote comparison might be missing one revision, possibly the first or last.

### 4. WmlComparer Table Revision Overcounting (1 failure)
**Failing Test:**
- WC-1500: Long-Table (expected 2, actual 10)

**Hypothesis:** Table row/cell changes may be counted multiple times instead of once.

### 5. Excel Currency Formatting (7 failures) - LIKELY .NET 10 ISSUE
**Pattern:** All currency symbols have extra "-" prefix
- Expected: "₩ -" → Actual: "-₩ -"
- Expected: "$ -" → Actual: "-$ -"

**Hypothesis:** This appears to be a .NET 10 number formatting change in how negative currency values are formatted. The extra "-" suggests the formatter is treating these as negative values.

**Severity:** Low - formatting display issue, not data corruption

### 6. SpreadsheetWriter Date Validation (1 failure) - VALIDATION ISSUE
**Test:** SW002_AllDataTypes
**Error:** Cell validation errors for dates '2012-01-09T00:00' and '2012-01-08T00:00'

**Hypothesis:** Date format validation rules changed in .NET 10 or OpenXML SDK updates.

### 7. PresentationBuilder Video (1 failure) - KNOWN ISSUE
**Test:** PB006_VideoFormats
**Error:** "Entries cannot be opened multiple times in Update mode"

**Hypothesis:** Zip package concurrency issue, possibly in how video parts are accessed.

### 8. PowerToolsBlock Extensions (1 failure) - RELATED TO OUR XDocument FIX
**Test:** MustEndPowerToolsBlockToUseStronglyTypedClasses
**Error:** Expected 1 paragraph but got 2

**Hypothesis:** Our XDocument cloning fix in WmlDocument.cs may have affected PowerTools block isolation. The test expects SDK and PowerTools changes to be isolated until EndPowerToolsBlock() is called, but they're now visible immediately.

**This may require adjusting our cloning approach or the block mechanism.**

### 9. DocumentBuilder TOC XML Error (1 failure) - UNRELATED
**Test:** DB009_ShredDocument
**Error:** XML parsing error at line 119 in ReferenceAdder.AddToc

**Hypothesis:** The XML template string formatting may be producing invalid XML. Could be related to special characters in the switches parameter or a pre-existing bug.

## Recommendations

### High Priority (Likely Fixable)
1. **WmlComparer Footnote/Endnote Issues** - 8 failures with clear pattern
   - Impact: HIGH (8 tests)
   - Difficulty: MEDIUM (requires understanding comparison algorithm)
   - Investigate footnote/endnote comparison logic in WmlComparer

2. **PowerToolsBlock Issue** - May be side effect of our fix
   - Impact: LOW (1 test)
   - Difficulty: MEDIUM (may need to refine our XDocument cloning)
   - Review if our cloning breaks PowerTools block isolation

### Medium Priority
3. **WmlComparer Image Relationship Issues** - 2 failures
   - Impact: MEDIUM (2 tests)
   - Difficulty: HIGH (relationship management is complex)

4. **WmlComparer Revision Count Issues** - 3 failures (off-by-one, overcounting)
   - Impact: LOW-MEDIUM (3 tests)
   - Difficulty: MEDIUM

### Low Priority (Likely Environmental/Framework Issues)
5. **Excel Currency Formatting** - 7 failures
   - Impact: LOW (display issue only)
   - Difficulty: LOW (update test expectations or formatter)
   - Likely .NET 10 behavior change

6. **SpreadsheetWriter Date Validation** - 1 failure
   - Impact: LOW
   - Difficulty: LOW

7. **PresentationBuilder Zip Access** - 1 failure
   - Impact: LOW
   - Difficulty: MEDIUM

8. **ReferenceAdder TOC XML** - 1 failure
   - Impact: LOW
   - Difficulty: LOW-MEDIUM
   - Appears to be pre-existing bug

## Next Steps

**Immediate Action:**
Investigate the 8 footnote/endnote comparison failures - they have a clear pattern and likely share a root cause. Fixing this could reduce failures from 20 to 12.

**Secondary:**
Review if our XDocument cloning affects PowerTools block isolation (1 failure).

**Later:**
Excel formatting issues are low-impact display problems that could be addressed by updating test expectations.
