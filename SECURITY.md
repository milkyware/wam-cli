# Security Policy

## Supported Versions

Only the latest release of `milkyware-wam-cli` published to [nuget.org](https://www.nuget.org/packages/milkyware-wam-cli) receives security fixes.

## Reporting a Vulnerability

Please do not open a public issue for a suspected vulnerability. Report it privately through GitHub's [private vulnerability reporting](https://github.com/milkyware/wam-cli/security/advisories/new).

Include:

- the affected version, as shown by `dotnet tool list --global`
- a description of the issue and its impact
- steps to reproduce, or a proof of concept when available

I will acknowledge the report and keep you updated as it is investigated, fixed, and disclosed.

## Scope

`milkyware-wam-cli` prints access tokens to standard output by design. A token exposed because the output was logged or redirected is not a vulnerability in the tool; revoke the token instead. Vulnerabilities in how the tool acquires, caches, or handles tokens are in scope.
