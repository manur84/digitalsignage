# Refactoring Quick Start Guide

## Top 3 Refactorings (Highest Impact)

### 1. Extract SendCommandAsync Helper (115 lines saved)
**File:** `ViewModels/DeviceManagementViewModel.cs`
**Target:** Lines 195-389 (7 methods)

Create one `ExecuteClientCommandAsync()` helper method and replace 7 identical methods.

**Time Estimate:** 30 minutes
**Risk:** LOW (single file, well-tested)
**Benefit:** 59% code reduction

```csharp
private async Task ExecuteClientCommandAsync(
    ClientCommands command,
    string actionName,
    Dictionary<string, object>? parameters = null)
{
    if (SelectedClient == null) return;
    try
    {
        var result = await _clientService.SendCommandAsync(
            SelectedClient.Id, command, parameters);

        if (result.IsFailure)
        {
            StatusMessage = $"Failed to {actionName}: {result.ErrorMessage}";
            return;
        }

        StatusMessage = $"{actionName} sent to {SelectedClient.Name}";
    }
    catch (Exception ex)
    {
        StatusMessage = $"Failed to {actionName}: {ex.Message}";
        _logger.LogError(ex, "Failed to {Action}", actionName);
    }
}
```

---

### 2. Create ViewModelExtensions (200+ lines saved)
**Create:** `Helpers/ViewModelExtensions.cs`
**Target:** 50+ error handling patterns across multiple ViewModels

Create `ExecuteSafeAsync()` extension method to consolidate try-catch-finally patterns.

**Time Estimate:** 1 hour
**Risk:** LOW (backward compatible)
**Benefit:** 40% error handling code reduction

```csharp
// File: Helpers/ViewModelExtensions.cs
public static async Task ExecuteSafeAsync(
    this ObservableObject viewModel,
    Func<Task> operation,
    ILogger logger,
    IDialogService dialogService,
    string operationName)
{
    try
    {
        await operation();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error in {Operation}", operationName);
        await dialogService.ShowErrorAsync(
            $"Failed to {operationName}:\n\n{ex.Message}",
            "Error");
    }
}
```

**Apply to:**
- `AlertsViewModel.cs` (20+ places)
- `DataSourceViewModel.cs` (8+ places)
- `DeviceManagementViewModel.cs` (remaining error handlers)

---

### 3. Extract Dialog Opening Helper (80 lines saved)
**File:** `ViewModels/MainViewModel.cs`
**Target:** Lines 107-224 (4 similar methods)

Create one `ShowDialogAsync<TViewModel, TDialog>()` generic helper.

**Time Estimate:** 45 minutes
**Risk:** LOW (single file, well-tested)
**Benefit:** 67% code reduction for dialog opening

```csharp
private async Task ShowDialogAsync<TViewModel, TDialog>(
    string dialogName,
    Action<TViewModel>? setup = null)
    where TViewModel : class
    where TDialog : System.Windows.Window, new()
{
    try
    {
        StatusText = $"Opening {dialogName}...";
        var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
        setup?.Invoke(viewModel);

        var dialog = new TDialog
        {
            DataContext = viewModel,
            Owner = System.Windows.Application.Current.MainWindow
        };

        dialog.ShowDialog();
        StatusText = $"{dialogName} closed";
    }
    catch (Exception ex)
    {
        StatusText = $"Error: {ex.Message}";
        await _dialogService.ShowErrorAsync($"Failed: {ex.Message}", $"{dialogName} Error");
    }
}
```

---

## Implementation Order (Week-by-Week Plan)

### Week 1
```
Monday-Wednesday:
  1. Create ExecuteClientCommandAsync() in DeviceManagementViewModel
  2. Test all 7 client commands
  3. Commit to git

Thursday-Friday:
  1. Create ViewModelExtensions.cs
  2. Apply to AlertsViewModel (20+ places)
  3. Test alert operations
  4. Commit to git
```

### Week 2
```
Monday-Wednesday:
  1. Apply ViewModelExtensions to DataSourceViewModel
  2. Apply to remaining error handlers
  3. Test all modified ViewModels
  4. Commit to git

Thursday-Friday:
  1. Create generic ShowDialogAsync<> in MainViewModel
  2. Refactor 4 dialog methods
  3. Test all dialogs
  4. Commit to git
```

### Week 3
```
Monday-Tuesday:
  1. Create CollectionExtensions.cs
  2. Apply ReplaceAll() to 12+ locations
  3. Test list operations
  4. Commit to git

Wednesday-Thursday:
  1. Create ValidationExtensions.cs
  2. Apply to AlertRuleEditorViewModel
  3. Apply to DataSourceViewModel
  4. Test all validation

Friday:
  1. Create WindowExtensions.cs
  2. Apply to dialog opening code
  3. Final testing
  4. Commit to git
```

---

## Checklist for Each Refactoring

### Before Starting
- [ ] Create feature branch: `git checkout -b refactor/extract-helpers`
- [ ] Run existing tests
- [ ] Note baseline code metrics

### During Refactoring
- [ ] Create helper class/method
- [ ] Add XML documentation
- [ ] Implement new pattern
- [ ] Maintain identical behavior (no logic changes)
- [ ] Add null checks where needed
- [ ] Update error messages if improved

### Testing
- [ ] Run all unit tests
- [ ] Test affected UI features manually
- [ ] Check for regressions
- [ ] Verify error handling still works
- [ ] Check logging still appears

### After Refactoring
- [ ] Delete old code
- [ ] Run tests one more time
- [ ] Commit with clear message
- [ ] Update code documentation if needed
- [ ] Verify next developer can understand changes

---

## Files to Create

1. **`/Helpers/ViewModelExtensions.cs`** (NEW)
   - `ExecuteSafeAsync()` - Generic error handler
   - `ExecuteWithLoadingAsync()` - Loading state wrapper
   - `ExecuteSafeAsync<T>()` - Generic with result type

2. **`/Helpers/CollectionExtensions.cs`** (NEW)
   - `ReplaceAll()` - Clear + add items
   - `AddRange()` - Add multiple items
   - `RemoveRange()` - Remove multiple items

3. **`/Helpers/ValidationExtensions.cs`** (NEW)
   - `ValidateRangeAsync()` - Range validation
   - `ValidateRequiredAsync()` - Required string validation
   - `ValidatePatternAsync()` - Regex validation

4. **`/Helpers/WindowExtensions.cs`** (NEW)
   - `SetAsChildOfMainWindow()` - Fluent window setup
   - `CenterOnScreen()` - Center window
   - `AsModal()` - Modal dialog setup

### Files to Modify

1. **`ViewModels/DeviceManagementViewModel.cs`**
   - Add `ExecuteClientCommandAsync()` helper
   - Replace 7 methods with calls to helper
   - Remove 115 lines

2. **`ViewModels/MainViewModel.cs`**
   - Add generic `ShowDialogAsync<>()` helper
   - Replace 4 dialog methods with calls to helper
   - Remove 80 lines

3. **`ViewModels/AlertsViewModel.cs`**
   - Use `ExecuteSafeAsync()` for error handling
   - Use `ReplaceAll()` for collection operations
   - Remove 60+ lines

4. **`ViewModels/DataSourceViewModel.cs`**
   - Use `ExecuteSafeAsync()` for error handling
   - Use `ValidateRangeAsync()` for validation
   - Remove 30+ lines

5. **(Optional) Remove `Helpers/RelayCommand.cs`**
   - Project already uses CommunityToolkit.Mvvm
   - This file is redundant
   - Remove 100 lines

---

## Code Review Checklist

When reviewing refactored code, verify:

- [ ] Error handling behavior is identical
- [ ] All try-catch semantics preserved
- [ ] Logging levels and messages match original
- [ ] Dialog owner is set correctly
- [ ] Collection operations produce same results
- [ ] Validation logic is unchanged
- [ ] No new null reference exceptions
- [ ] No silent failures introduced
- [ ] Performance is same or better
- [ ] Code is more readable than before

---

## Rollback Strategy

If issues arise:

```bash
# Last commit has issues?
git log --oneline -5
git revert <commit-hash>  # Revert specific commit
git push origin

# Or entire feature branch failed?
git reset --hard HEAD~5   # Reset last 5 commits
git push -f origin
```

---

## Success Criteria

✓ **Code Quality**
- Cyclomatic complexity reduced by 15%
- Duplicated lines reduced by 500+
- All tests passing

✓ **Maintainability**
- New helper methods documented
- Helper methods have 100% usage
- No orphaned methods

✓ **Performance**
- No performance regression
- Reduced memory allocations
- Build time unchanged

✓ **Team**
- Clear commit messages
- Code review comments addressed
- Documentation updated

---

## Common Pitfalls to Avoid

❌ **Don't:**
- Change logic while refactoring (split into separate PR)
- Forget null checks in new helpers
- Remove error handling
- Rename exceptions
- Change logging levels

✓ **Do:**
- Keep refactoring PRs focused
- Test thoroughly before committing
- Maintain backward compatibility
- Document helper methods
- Use same error messages as original

---

## Estimated Impact

| Metric | Before | After | Change |
|--------|--------|-------|--------|
| **Lines of Code** | 2,500+ | 2,000 | -20% |
| **Duplicated Lines** | 500+ | 0 | -100% |
| **Code Duplication %** | 12-15% | 2-3% | -80% |
| **Cyclomatic Complexity** | High | Medium | Reduced |
| **Maintainability Index** | 65/100 | 75/100 | +15% |

---

## Questions? References

- See `CODE_DUPLICATION_ANALYSIS.md` for detailed analysis
- See `REFACTORING_EXAMPLES.md` for detailed code examples
- Check CLAUDE.md for code style guidelines
- See WORK.md for known issues

