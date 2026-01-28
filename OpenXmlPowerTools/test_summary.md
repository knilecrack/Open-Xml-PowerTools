# Test Results Summary

## Overall Status
- **Total Tests:** 979
- **Passed:** 956 (97.6%)
- **Failed:** 22 (2.2%)
- **Skipped:** 1 (0.1%)

## Test Failures by Category

### 1. PowerToolsBlockExtensionsTests (1 failure)
**Test:** `MustEndPowerToolsBlockToUseStronglyTypedClasses`
**Error:** Assert.Single() failure - collection contained 2 items instead of 1
**Severity:** Low - appears to be a test assertion issue

### 2. Excel Currency Formatting - CfTests (7 failures)
**Issue:** Currency symbols have extra "-" prefix
- Expected: "₩ -" → Actual: "-₩ -"
- Expected: "£ -" → Actual: "-£ -"
- Expected: "¥ -" → Actual: "-¥ -"
- Expected: "€  -" → Actual: "-€  -"
- Expected: "CHF  -" → Actual: "-CHF  -"
- Expected: "$ -" → Actual: "-$ -"

**Severity:** Medium - Excel number formatting inconsistency

### 3. SpreadsheetWriter Tests (1 failure)
**Test:** `SW002_AllDataTypes`
**Error:** Cell validation errors for date values ('2012-01-09T00:00', '2012-01-08T00:00')
**Severity:** Medium - date formatting validation issue

### 4. PresentationBuilder Tests (1 failure)
**Test:** `PB006_VideoFormats`
**Error:** `System.IO.IOException: Entries cannot be opened multiple times in Update mode`
**Severity:** Medium - PowerPoint package access issue

### 5. DocumentBuilder Tests (1 failure)
**Test:** `DB009_ShredDocument`
**Error:** `System.Xml.XmlException: Data at the root level is invalid. Line 57, position 9`
**Location:** ReferenceAdder.cs:119 in AddToc method
**Severity:** Medium - XML parsing issue in TOC generation

### 6. WmlComparer Tests (11 failures)
Most critical category with multiple types of failures:

#### Type A: Duplicate FontTable Relationship (3 failures)
- WC002_Consolidate_Bulk_Test (RC/RC002-Image.docx)
- WC003_Compare (RC/RC002-Image.docx)
- WC001_Consolidate (RC/RC002-Image.docx)

**Error:** "The package/part 'MainDocumentPart{/word/document.xml}' can only have one instance of relationship that targets part fontTable"
**Severity:** HIGH - package relationship corruption issue

#### Type B: Missing/Wrong Revision Count (6 failures)
- WC-1720: Expected 7, Actual 6
- WC-1710: Expected 7, Actual 6
- WC-1500: Expected 2, Actual 10
- WC-1660: Expected 5, Actual 0
- WC-1670: Expected 5, Actual 0
- WC-1750: Expected 6, Actual 0
- WC-1760: Expected 6, Actual 0

**Severity:** MEDIUM to HIGH - comparison algorithm accuracy issues

#### Type C: Invalid PackageRelationship (1 failure)
- WC-1240: "PackageRelationship with specified ID does not exist for the source part"
**Error location:** WmlComparer.cs:4753 in MoveRelatedPartsToDestination
**Severity:** HIGH - missing relationship handling issue

## Analysis

### Known Issues (from your list):
1. ✅ **FIXED:** SplitOnSections body-level w:sectPr - This was addressed in our recent commit
2. ⚠️ **REMAINING:** AdjustSectionBreak removes wrong w:sectPr
3. ⚠️ **REMAINING:** MainDocumentPart/Comments part XDocument mutation

### Newly Identified Issues:
1. **WmlComparer FontTable Duplication** - Multiple relationship instances being created (HIGH priority)
2. **WmlComparer Revision Counting** - Incorrect revision detection in footnote/endnote scenarios
3. **WmlComparer Relationship Handling** - Missing relationships in image comparison scenarios
4. **ReferenceAdder TOC XML** - Invalid XML generation
5. **PresentationBuilder Zip Access** - Concurrent file access issue
6. **Excel Formatting** - Currency/date formatting issues

### Recommendations:
1. **Immediate Priority:** Fix WmlComparer duplicate fontTable relationship issue (affects 3 tests)
2. **High Priority:** Fix AdjustSectionBreak (known issue #2)
3. **High Priority:** Fix XDocument mutation (known issue #3)
4. **Medium Priority:** Address WmlComparer revision counting accuracy
5. **Lower Priority:** Excel formatting and PresentationBuilder issues

## Next Steps
Would you like me to:
1. Investigate the WmlComparer fontTable duplication issue?
2. Fix the second known issue (AdjustSectionBreak)?
3. Analyze a specific test failure category?
