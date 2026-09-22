# Security Policy

## Reporting a vulnerability

If you discover a security vulnerability in Deadzone, **do not** open a public GitHub issue with exploit details.

Use **GitHub's private vulnerability reporting** (Security Advisories) instead:

1. Go to the Deadzone repository on GitHub.
2. Click the **Security** tab.
3. Click **Report a vulnerability**.
4. Describe the issue privately.

When reporting, please include:

- The affected Deadzone version
- A description of the vulnerability
- Steps to reproduce the issue
- The potential impact

**Do not include** passwords, certificates, private keys, or any personal information in your report.

## Scope

Deadzone is a local desktop utility that does not make network requests, does not store account credentials, and does not transmit telemetry. Security reports are most relevant to:

- Privilege escalation via the administrator-elevated process
- Unsafe handling of controller input or settings data
- Unintended interaction with HIDMaestro or other system components

## Supported versions

| Version | Supported |
|---------|-----------|
| 0.1.0   | ✅ Yes    |

Only the latest release receives security fixes.
