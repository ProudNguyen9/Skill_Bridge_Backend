# Resend email setup

The backend sends verification and password-reset messages through the Resend Email API. Plesk SMTP settings are only used by the optional `scripts/test-smtp.mjs` diagnostic script.

## Configure Resend

1. In Resend, add the sending domain `phelieuminhduc.com` (or a dedicated sending subdomain) and copy the DNS records Resend displays.
2. Add those records in the DNS provider that owns the domain's nameservers. For this domain, public DNS currently points to GoDaddy nameservers, so add the records in GoDaddy. Do not overwrite the existing Plesk DKIM records unless Resend explicitly gives the same host and instructs you to replace it.
3. Verify the domain in Resend. Create an API key with permission to send email.
4. Set the following in the ignored local `Backend/.env` or the deployment secret store:

```dotenv
Email__Enabled=true
Email__ApiKey=re_replace_with_your_resend_key
Email__FromAddress=noreply@phelieuminhduc.com
Email__FromName=DNTU SkillBridge
App__FrontendBaseUrl=http://localhost:5173
```

Keep the real API key out of Git and chat. Restart the backend after changing configuration.

## Verify delivery

Use registration or password recovery for a test account, then check Resend's Emails/Logs page for the message status. A successful API response means Resend accepted the request; check the event status for delivered, bounced, or failed. Verification and reset links point to `/verify-email?token=...` and `/reset-password?token=...` on `App__FrontendBaseUrl`.

If Resend rejects the request, the backend reports the HTTP status in its error. Common setup issues are an unverified sending domain, an invalid API key, or a `FromAddress` outside the verified domain.
