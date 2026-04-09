# JWT Session Issues - Investigation Notes

**Date:** 2026-04-09  
**Status:** Investigation Needed

---

## Symptoms

1. Knowledge tab shows "Not authenticated"
2. Plan not assigned
3. No org/company shown
4. User re-logged in but issue persists

---

## Context

- **JWT Lifetime**: Reduced from 7 days to 60 minutes (F-01 fix)
- **Session Strategy**: NextAuth uses `jwt` strategy
- **Issue**: No refresh token mechanism exists

---

## Suspected Root Cause

When the backend JWT (60 min) expires but NextAuth session is still valid:
- Frontend shows as "logged in" (NextAuth session)
- Backend API calls fail (JWT expired)
- No mechanism to refresh the expired JWT
- User sees "Not authenticated" on protected pages

---

## Auth Flow

```
┌─────────────┐    login     ┌─────────────┐
│  Frontend   │ ───────────►  │   Backend   │
│ (NextAuth)  │               │  (JWT 60m)  │
└─────────────┘               └─────────────┘
      │                               │
      │ session                      │ token (embedded in session.apiToken)
      ▼                               ▼
┌─────────────┐               ┌─────────────┐
│  Browser    │               │  API Calls  │
│  Session    │ ────────►    │  (fails!)   │
│  (valid)    │   apiToken    │  JWT expired│
└─────────────┘               └─────────────┘
```

---

## Investigation Steps

1. **Check Network tab** when clicking Knowledge page
   - Status code from `/api/v1/knowledge`
   - Error message response

2. **Check browser DevTools → Application → Session Storage**
   - Is `nextauth.session-token` present?
   - Is `apiToken` in session?

3. **Test API directly** with cURL:
   ```bash
   curl http://localhost:8080/api/v1/knowledge \
     -H "Authorization: Bearer <token>"
   ```

---

## Proposed Solution

### Option A: Refresh Token Endpoint (Recommended)
- Add `POST /api/auth/refresh` endpoint
- Returns new JWT in exchange for valid (but possibly expired) JWT
- Add refresh logic to NextAuth `jwt` callback

### Option B: Increase JWT Lifetime
- Temporarily extend JWT to 24 hours (not recommended for production)

### Option C: Silent Re-authentication
- On 401, redirect to login or trigger silent token refresh

---

## Files to Modify

| File | Change |
|------|--------|
| `AuthController.cs` | Add `/refresh` endpoint |
| `auth.ts` | Add token refresh logic in jwt callback |
| `middleware.ts` (Next.js) | Handle 401 and refresh token |

---

## Related Security Fix

- **F-01**: JWT lifetime shortened to 60 min
- This change introduced the need for refresh mechanism

---

## Recent Fixes Applied

- **2026-04-09**: Fixed Swagger crash by adding `[NonAction]` to Hangfire job methods in `MailboxConnectionsController.cs`
- **2026-04-09**: Added uploads volume mount to `docker-compose.yml` for file persistence
