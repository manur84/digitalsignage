# Security Improvements - November 2024

## Overview
This document describes the security improvements implemented to enhance the security posture of the Digital Signage system.

## 1. Password Security

### Password Policy
Implemented configurable password policy with the following default requirements:
- Minimum length: 8 characters
- Maximum length: 128 characters (prevents DoS attacks)
- Requires at least one uppercase letter
- Requires at least one lowercase letter
- Requires at least one digit
- Requires at least one special character

**Location:** `src/DigitalSignage.Core/Security/PasswordPolicy.cs`

### Password Hashing
- ✅ **BCrypt** with work factor 12 for password hashing (already implemented)
- BCrypt is resistant to rainbow table and brute-force attacks
- Work factor 12 provides strong security while maintaining acceptable performance

**Implementation:** `AuthenticationService.HashPassword()` and `VerifyPassword()`

## 2. Account Lockout Protection

### Account Lockout Policy
Implemented automatic account lockout to prevent brute-force attacks:
- Default: 5 failed login attempts triggers lockout
- Lockout duration: 15 minutes (configurable)
- Failed attempts window: 15 minutes (configurable)
- Automatic unlock after lockout period expires

**Location:** `src/DigitalSignage.Core/Security/AccountLockoutPolicy.cs`

### Database Changes
Added new fields to User entity:
- `FailedLoginAttempts` - Counter for failed login attempts
- `LastFailedLoginAt` - Timestamp of last failed login
- `LockedUntil` - Account locked until this timestamp

**Migration:** `20251124000000_AddAccountLockoutFields.cs`

## 3. Security Event Logging

### Centralized Security Logging
Implemented `SecurityEventLogger` for comprehensive security event tracking:
- Authentication successes and failures
- Account lockouts
- Password changes
- API key operations
- Registration token operations
- Suspicious activities
- Rate limit violations

**Location:** `src/DigitalSignage.Core/Security/SecurityEventLogger.cs`

### Logged Events
All security-critical operations are now logged with structured logging:
- `[SECURITY]` prefix for easy filtering
- Event type classification
- Contextual information (username, IP address, etc.)
- Timestamps (automatic via logging framework)

## 4. Timing Attack Prevention

### Constant-Time Operations
Implemented constant-time delays to prevent timing attacks:
- 100ms delay on authentication failures (prevents user enumeration)
- Same error message for "user not found" and "wrong password"
- Consistent response times regardless of failure reason

**Implementation:** `AuthenticationService.AuthenticateAsync()`

## 5. Input Validation Enhancements

### C# Server-Side Validation

#### Path Traversal Protection
- ✅ Already implemented via `PathHelper.IsValidFileName()`
- Prevents `../` attacks
- Validates filenames don't contain directory separators
- Used in media file operations

**Location:** `src/DigitalSignage.Core/Utilities/PathHelper.cs`

#### SQL Injection Protection
- ✅ Already implemented via parameterized queries (Dapper)
- Connection string sanitization via `ConnectionStringHelper`
- Whitelist-based approach for connection string properties

**Location:** `src/DigitalSignage.Core/Utilities/ConnectionStringHelper.cs`

#### Registration Token Validation (Python Client)
New validation for registration tokens received from server:
- Only alphanumeric characters and hyphens allowed
- Length between 8-128 characters
- No whitespace or special characters
- Prevents injection attacks

**Location:** `src/DigitalSignage.Client.RaspberryPi/config.py::_is_valid_registration_token()`

## 6. Existing Security Features (Verified)

### Cryptographically Secure Random Number Generation
- ✅ Uses `RandomNumberGenerator.Create()` for tokens and API keys
- ✅ High-entropy random values
- ✅ Suitable for cryptographic purposes

### API Key Security
- ✅ BCrypt hashing with work factor 10
- ✅ 32-character random API keys
- ✅ Stored as hashes only (never in plaintext)
- ✅ Rate limiting for validation attempts

### SSL/TLS Enforcement
- ✅ WSS (WebSocket Secure) only mode
- ✅ Server rejects non-SSL connections
- ✅ Self-signed certificate auto-generation
- ✅ Certificate management service

### Rate Limiting
- ✅ Implemented for authentication attempts
- ✅ Implemented for API key validation
- ✅ Prevents brute-force attacks
- ✅ Configurable limits

### Subprocess Security (Python Client)
- ✅ All subprocess calls use list format (not shell=True)
- ✅ No user input passed to shell
- ✅ No eval/exec/pickle usage
- ✅ Safe command execution

## 7. Hash Algorithm Usage (Verified Safe)

### SHA-256 Usage
- ✅ Used for file integrity checks (media files)
- ✅ NOT used for password hashing
- ✅ Appropriate use case for non-cryptographic hashing

### SHA-1 Usage
- ✅ Used only for WebSocket handshake (RFC 6455 requirement)
- ✅ NOT used for security-critical operations
- ✅ Protocol-mandated usage

### MD5 Usage
- ✅ Used only for deterministic GUID generation
- ✅ NOT used for password hashing or security
- ✅ Appropriate for non-security purposes

## 8. Security Configuration

### Default Security Settings
The system ships with secure defaults:
- Password policy: Strong (8+ chars, mixed case, digits, special chars)
- Account lockout: Enabled (5 attempts, 15 min lockout)
- Rate limiting: Enabled
- SSL/TLS: Required
- Password hashing: BCrypt work factor 12
- API key hashing: BCrypt work factor 10

### Configurable Policies
Organizations can customize security policies:
```csharp
// Example: Lenient password policy for development
var lenientPolicy = PasswordPolicy.Lenient;

// Example: Disabled account lockout for testing
var noLockout = AccountLockoutPolicy.Disabled;
```

## 9. Security Recommendations

### For Production Deployments
1. ✅ Keep default strong password policy
2. ✅ Keep account lockout enabled
3. ✅ Use SSL/TLS with valid certificates (not self-signed)
4. ✅ Monitor security event logs regularly
5. ✅ Implement password expiration if required by policy
6. ✅ Regularly rotate API keys
7. ✅ Use registration tokens with expiration dates

### For Development/Testing
- Consider using `PasswordPolicy.Lenient` for easier testing
- Consider using `AccountLockoutPolicy.Disabled` to prevent lockouts during testing
- Use self-signed certificates (already default)
- Review security logs for debugging

## 10. Security Audit Summary

### ✅ Strengths
- Strong password hashing (BCrypt)
- Comprehensive input validation
- SQL injection protection
- Path traversal protection
- Rate limiting
- SSL/TLS enforcement
- Security event logging
- Account lockout protection
- Timing attack prevention
- Safe subprocess execution

### 🔒 Areas for Future Enhancement
1. Multi-factor authentication (MFA)
2. Password expiration policies
3. Session management improvements
4. IP-based access control
5. CSRF protection for web interfaces
6. Content Security Policy headers
7. Regular security audits
8. Automated vulnerability scanning
9. Security testing suite
10. Intrusion detection system

## 11. Compliance Considerations

The implemented security measures help with:
- **GDPR**: Password security, access control, audit logging
- **NIST Cybersecurity Framework**: Authentication, access control, monitoring
- **OWASP Top 10**: Protection against injection, broken authentication, security misconfiguration

## 12. Testing Security Features

### Password Policy Testing
```csharp
var policy = PasswordPolicy.Default;
bool isValid = policy.ValidatePassword("Test123!", out string? error);
// isValid = true

bool isWeak = policy.ValidatePassword("weak", out error);
// isWeak = false, error = "Password must be at least 8 characters long"
```

### Account Lockout Testing
1. Attempt login with wrong password 5 times
2. Account should be locked for 15 minutes
3. Login should fail with "Account is locked" message
4. After 15 minutes, account should automatically unlock

### Security Event Log Testing
Check logs for `[SECURITY]` events:
```bash
grep "\[SECURITY\]" logs/digitalsignage.log
```

## 13. Security Contact

For security issues or vulnerabilities, please:
1. Do NOT create public GitHub issues
2. Contact the development team privately
3. Provide detailed information about the vulnerability
4. Allow time for patching before public disclosure

## 14. Version History

- **v1.0 (Nov 2024)**: Initial security improvements
  - Password policy enforcement
  - Account lockout protection
  - Security event logging
  - Input validation enhancements
  - Timing attack prevention

---

**Last Updated:** November 24, 2024
