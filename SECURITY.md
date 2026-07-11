# Security Policy

## Supported Versions

Security fixes are handled on the latest released version of Multi WebView.

## Reporting A Vulnerability

Please report security issues through GitHub Issues unless the issue contains private account data, cookies, tokens, or other sensitive information. If sensitive details are required, contact the maintainer privately before sharing reproduction data.

Do not include real WebView2 profile folders, cookies, screenshots, or account session files in public issues.

## Privacy And Local Data

Multi WebView stores profile metadata, screenshots, settings, and WebView2 browser data locally under:

```text
%LOCALAPPDATA%\MultiWebView
```

Each app profile uses a separate WebView2 user data folder. That isolation prevents profiles from sharing cookies and local browser state with each other, but the app does not encrypt this data itself.

The installer uninstaller removes installed application files and shortcuts only. It intentionally leaves `%LOCALAPPDATA%\MultiWebView` in place so user browser profiles, cookies, sessions, and screenshots are not deleted unexpectedly.

## Public Sharing Checklist

Before publishing screenshots, logs, sample profile folders, or reproduction projects, check that they do not contain:

- account names or email addresses
- cookies or browser sessions
- access tokens, API keys, or passwords
- screenshots with private pages
- custom profile storage paths that expose private directory names
