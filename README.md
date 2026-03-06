# jcoliz.FunctionalTests

A .NET library providing base classes and infrastructure for Playwright-based functional tests using NUnit.

## Overview

This library offers a foundation for writing browser-based functional tests with Playwright. It handles common concerns like test lifecycle management, page object model patterns, shared state between test steps, test-to-backend correlation via distributed tracing, and flexible parameter resolution from environment variables and .runsettings files.

## Key Components

### FunctionalTest

Abstract base class for all functional tests. Inherits from Playwright's `PageTest` and provides:

- **Automatic browser configuration** — sets viewport size (1280×720), enables downloads, and configures the base URL from test parameters
- **Test correlation** — creates a `TestCorrelationContext` per test that attaches W3C `traceparent` and custom `X-Test-*` headers to every browser request, linking frontend actions to backend logs
- **Object store** — initializes a fresh `ObjectStore` per test for sharing data between steps
- **Page object caching** — `GetOrCreatePage<T>()` creates page objects on first access and reuses them across steps within a test
- **Failure screenshots** — automatically captures a screenshot when a test fails
- **Parameter resolution** — `GetRequiredParameter()` reads values from .runsettings and resolves `{ENV_VAR}` references, with automatic loading of .env files
- **Configurable timeouts** — supports a `defaultTimeout` test parameter to override Playwright's default timeout

### PageObjectModel

Base class for page object models, providing cross-cutting browser interaction helpers:

- **Navigation** — `LaunchSite()` navigates to the site root
- **Page title** — `GetPageTitle()` retrieves the current browser page title
- **Availability checking** — `IsAvailableAsync()` checks if a control is both visible and enabled, useful for permission-based UI scenarios
- **Hydration waiting** — `WaitForEnabled()` polls until an element becomes enabled, handling client-side framework hydration delays
- **API synchronization** — `WaitForApi()` executes an action and waits for a matching API response by URL pattern
- **Screenshots** — `SaveScreenshotAsync()` captures screenshots with organized file naming based on test class, test name, and an optional moment identifier

### ObjectStore

A simple typed dictionary for sharing objects between test steps within a single test execution. Objects can be stored and retrieved by type name (automatic key) or by explicit string key. This eliminates the need for local variables when composing test steps from multiple step classes.

### TestCorrelationContext

Manages a `System.Diagnostics.Activity` that links functional test execution to backend API logs via distributed tracing. Generates both standard W3C `traceparent` headers and custom `X-Test-Name`, `X-Test-Id`, and `X-Test-Class` headers for correlation.

## Dependencies

- **Microsoft.Playwright.NUnit** — Playwright test integration for NUnit
- **NUnit** — Test framework
- **DotNetEnv** — .env file loading for environment variable configuration

## Target Framework

.NET 10

## Usage

Inherit from `FunctionalTest` in your test project to get all the base infrastructure. Create page object models by inheriting from `PageObjectModel`. Use the `ObjectStore` to share state between test steps.

Your test project should provide .runsettings files with at minimum a `webAppUrl` parameter pointing to the application under test. Parameter values can reference environment variables using `{VAR_NAME}` syntax, which will be resolved from the process environment or a .env file.

## License

See the repository for license information.
