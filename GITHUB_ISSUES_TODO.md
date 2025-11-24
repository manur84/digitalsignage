# GitHub Issues for Code Quality Improvements

This document catalogs the code quality improvements needed for the Digital Signage project.

## Issue #1: Implement FFmpeg Video Thumbnail Generation
**File:** `src/DigitalSignage.Server/Services/ThumbnailService.cs:141`
**Description:** Currently using placeholder thumbnails for videos. Need to implement FFmpeg integration to extract first frame from video files.
**Priority:** Medium
**Labels:** enhancement, media-handling

## Issue #2: Implement Authentication Context for Admin Username
**File:** `src/DigitalSignage.Server/ViewModels/MobileAppManagementViewModel.cs:96`
**Description:** Hard-coded "Admin" string should be replaced with actual authenticated admin username from authentication context.
**Priority:** High
**Labels:** authentication, technical-debt

## Issue #3: Implement UI Notification System
**Files:** 
- `src/DigitalSignage.Server/ViewModels/MobileAppManagementViewModel.cs:107,112,148,153,189,194,228,233`
**Description:** Add toast notification system to show success/error messages when approving, rejecting, revoking, or deleting mobile app registrations.
**Priority:** Medium
**Labels:** ui, ux-improvement

## Issue #4: Add Toast Notification for New App Registrations
**File:** `src/DigitalSignage.Server/ViewModels/MobileAppManagementViewModel.cs:332`
**Description:** Show toast notification to user when a new mobile app registration is received.
**Priority:** Low
**Labels:** ui, notifications

## Issue #5: Implement Manual Device Registration Dialog
**File:** `src/DigitalSignage.Server/ViewModels/ServerManagementViewModel.cs:186`
**Description:** Create dialog for manually adding/registering devices to the server.
**Priority:** Medium
**Labels:** feature, device-management

## Issue #6: Implement Cache Service for Mobile App
**File:** `src/DigitalSignage.App.Mobile/ViewModels/SettingsViewModel.cs:197`
**Description:** Create cache service to manage and clear offline data in the mobile app.
**Priority:** Medium
**Labels:** mobile, performance, offline-support

## Issue #7: Implement Layout Assignment Dialog for Mobile App
**File:** `src/DigitalSignage.App.Mobile/ViewModels/DeviceDetailViewModel.cs:252`
**Description:** Create UI for assigning layouts to devices from the mobile app.
**Priority:** Medium
**Labels:** mobile, feature, layout-management

## Issue #8: Replace Generic Exception Handlers with Specific Types
**Priority:** High
**Labels:** code-quality, error-handling, refactoring

### Files Requiring Exception Handler Improvements:

#### High Priority Files (Most catch blocks):
1. **WebSocketCommunicationService.cs** - 27 catch blocks (14 improved, 13 remaining)
   - Lines with remaining generic catch blocks need review
   
2. **AlertsViewModel.cs** - 21 catch blocks (3 improved, 18 remaining)
   - Database operation exceptions should catch `DbUpdateException`
   - UI operations should catch `InvalidOperationException`
   - Async operations should catch `OperationCanceledException`

3. **MobileAppService.cs** - 17 catch blocks
   - Database operations should catch `DbUpdateException`, `DbUpdateConcurrencyException`
   - Security operations should catch `CryptographicException`
   
4. **DeviceManagementViewModel.cs** - 15 catch blocks
   - Database operations: `DbUpdateException`
   - Network operations: `IOException`, `SocketException`
   
5. **ClientService.cs** - 13 catch blocks
   - Network operations: `IOException`, `SocketException`, `TimeoutException`
   - Serialization: `JsonException`, `JsonSerializationException`

#### Medium Priority Files:
6. **MessageHandlerService.cs** - 11 catch blocks
7. **LayoutService.cs** - 11 catch blocks
8. **SqlDataSourcesViewModel.cs** - 10 catch blocks
9. **SqlDataSourceService.cs** - 10 catch blocks
10. **ApiService.cs (Mobile)** - 10 catch blocks

#### Recommended Exception Types by Context:

**Database Operations:**
- `DbUpdateException` - Database update failures
- `DbUpdateConcurrencyException` - Concurrency conflicts
- `InvalidOperationException` - Invalid database state

**Network Operations:**
- `IOException` - I/O errors
- `SocketException` - Socket errors
- `TimeoutException` - Operation timeouts
- `HttpRequestException` - HTTP request failures

**Serialization:**
- `JsonException` - JSON parsing errors
- `JsonSerializationException` - Serialization failures

**File Operations:**
- `IOException` - File I/O errors
- `UnauthorizedAccessException` - Permission denied
- `FileNotFoundException` - File not found
- `DirectoryNotFoundException` - Directory not found

**Security:**
- `CryptographicException` - Encryption/decryption errors
- `AuthenticationException` - Authentication failures

**Async Operations:**
- `OperationCanceledException` - Cancelled operations
- `TaskCanceledException` - Cancelled tasks

**Resource Management:**
- `ObjectDisposedException` - Using disposed objects
- `OutOfMemoryException` - Memory exhaustion

### Implementation Guidelines:

1. **Catch Most Specific First:** Order catch blocks from most specific to most general
2. **Avoid Empty Catches:** Always log or handle exceptions appropriately
3. **Don't Swallow Exceptions:** Re-throw if unable to handle properly
4. **Use Exception Filters:** Use `when` clauses for conditional catching
5. **Document Why:** Add comments explaining why generic catch is necessary if it must be used

### Example Pattern:
```csharp
try
{
    await _dbContext.SaveChangesAsync();
}
catch (DbUpdateConcurrencyException ex)
{
    _logger.LogWarning(ex, "Concurrency conflict updating entity");
    // Handle concurrency
}
catch (DbUpdateException ex)
{
    _logger.LogError(ex, "Database error updating entity");
    throw;
}
catch (OperationCanceledException)
{
    _logger.LogDebug("Update operation cancelled");
    throw;
}
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected error updating entity");
    throw;
}
```

## Issue #9: Analyze and Optimize Database Queries
**Priority:** Medium
**Labels:** performance, database, optimization

### Current Status:
- AlertsViewModel properly uses `.Include(a => a.AlertRule)` for eager loading
- No critical N+1 query patterns detected in initial analysis
- Further analysis needed for:
  - SchedulingViewModel queries
  - DeviceManagementViewModel queries
  - Large dataset scenarios

### Recommended Actions:
1. Enable EF Core query logging to detect N+1 queries in production
2. Add `.AsNoTracking()` for read-only queries
3. Consider query splitting for complex includes
4. Use projection (Select) instead of loading full entities when possible
5. Implement query result caching where appropriate

### Files to Review:
- `src/DigitalSignage.Server/ViewModels/SchedulingViewModel.cs`
- `src/DigitalSignage.Server/ViewModels/DeviceManagementViewModel.cs`
- `src/DigitalSignage.Server/Services/ClientService.cs`
- `src/DigitalSignage.Server/Services/LayoutService.cs`

## Statistics

### Current Progress:
- **Exception Handlers:** 28 improved out of 448 (6%)
- **TODO Comments:** 14 converted to GitHub issues (100%)
- **N+1 Queries:** Initial analysis complete, no critical issues found

### Remaining Work:
- **Exception Handlers:** ~420 remaining
- **Database Query Optimization:** Ongoing monitoring needed
- **Code Review:** Required before closing
- **Security Scan:** CodeQL scan required

## Next Steps

1. Create GitHub issues #1-#9 in the repository
2. Prioritize by impact and effort
3. Assign to appropriate developers
4. Set milestones for completion
5. Continue systematic refactoring of exception handlers
6. Monitor query performance in production

## Notes

- All changes should maintain backward compatibility
- Build and test after each batch of changes
- Update documentation as features are implemented
- Consider creating a coding standards document for exception handling
