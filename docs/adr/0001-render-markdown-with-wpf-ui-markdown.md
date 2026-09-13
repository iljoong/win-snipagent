# 0001: Render AI answers with WPF-UI.Markdown

**Status:** Accepted
**Date:** 2026-09-13
**Supersedes:** None
**Superseded by:** None

## Context

The AI answer overlay must render untrusted Markdown as selectable native WPF
content. It must support common Markdown, tables, and task lists while blocking
image retrieval, showing raw HTML literally, and allowing only user-activated
HTTP(S) links. Wide code blocks and tables require local horizontal scrolling,
and renderer styling must remain scoped to the overlay.

This decision meets the ADR threshold because it introduces a significant
rendering dependency at a security boundary and affects accessibility, theming,
reliability, and future replacement cost.

## Decision drivers

- Native WPF output with standard text selection and copying.
- Explicit application control over links, images, raw HTML, and external I/O.
- Compatibility with `net10.0-windows10.0.19041.0`.
- Support for the approved Markdown syntax without a browser surface.
- Overlay-local styling without application-wide theme changes.
- A maintained parser and renderer that can be constrained without a fork.

## Options considered

### WPF-UI.Markdown with an application-owned safe renderer

WPF-UI.Markdown uses Markdig and emits a WPF `FlowDocument`, which supports
selection and copying. Its public `WpfRenderer` is subclassable and its object
renderer collection can be replaced with application-owned renderers for links,
images, HTML, code blocks, and tables.

The package also brings WPF-UI and ColorCode dependencies that SnipAgent does
not otherwise use. Its default image renderer may resolve external URIs and its
default link command is broader than the approved policy, so the default
renderer and viewer behavior cannot be used for untrusted answers.

### MdXaml

MdXaml is mature, MIT-licensed, and emits selectable WPF `FlowDocument`
content. Version 1.27.0 supports tables and restores successfully for modern WPF
targets.

Its image path recognizes HTTP, HTTPS, file, pack, and data URIs and performs
loading through an internal manager. Disabling all image access and replacing
images with deterministic alt-text indicators would require brittle
preprocessing or a maintained fork.

### MarkdownViewer.Wpf

MarkdownViewer.Wpf targets .NET 10 directly, uses Markdig, and exposes
application services for link navigation and image resolution. Its renderer is
modular and well suited to explicit security policies.

It produces a native `UIElement` tree whose paragraph content is based on WPF
`TextBlock` controls rather than a `FlowDocument`. That does not provide the
approved standard selection and copying behavior across rendered content. The
package is also new and has limited adoption.

### Markdig with a fully application-owned FlowDocument renderer

Using Markdig alone would minimize UI-framework dependencies and give the
application complete control over its security boundary.

It would also require SnipAgent to implement and maintain the full WPF renderer,
including lists, tables, task lists, nested inline formatting, selection,
styling, accessibility, and overflow behavior. That is substantially more
feature code and validation than constraining an existing renderer.

## Decision

Use the NuGet package `WPF-UI.Markdown` version `4.0.2`, pinned explicitly, and
build an application-owned safe renderer subclass around its Markdig-to-WPF
infrastructure.

Do not use the package's default Markdown viewer or default renderer for AI
answers. The application-owned renderer must register only the required
renderers and must:

- emit a selectable `FlowDocument`;
- permit navigation only for user-activated absolute HTTP(S) links;
- replace every image with a non-interactive blocked-image indicator containing
  its alt text, without resolving the URI;
- render raw HTML source literally;
- provide local horizontal scrolling for wide code blocks and tables;
- use overlay-local resources and avoid global WPF-UI theme dictionaries; and
- surface rendering failures so the overlay can show its explicit plain-text
  fallback while preserving the original answer.

The repository owner explicitly selected this option on 2026-09-13.

## Consequences

### Positive

- The answer remains native WPF content with standard selection and copying.
- Markdig supplies the approved Markdown parsing features.
- Application-owned object renderers make external-I/O policy explicit and
  independently testable.
- Existing overlay behavior and the original saved Markdown remain separate
  from presentation.

### Negative

- SnipAgent gains transitive WPF-UI, Markdig, and ColorCode dependencies for one
  feature.
- The package's defaults are unsafe for this input and must never be used as a
  convenience fallback.
- NuGet 4.0.2 differs from the current repository source: the repository
  exposes a `HyperlinkInteractive` viewer property that is not present in the
  shipped package. Implementation must be based on the pinned package API.
- Custom link, image, HTML, code-block, and table renderers require focused
  security and visual regression tests.

### Follow-up

- Keep the renderer behind an application-owned presentation boundary.
- Add tests proving blocked content cannot trigger network, file, package, data
  URI, or shell access.
- Review dependency updates explicitly rather than using floating versions.
- Reconsider this decision if the package becomes unmaintained, removes its
  renderer extension points, or a mature lightweight FlowDocument renderer
  provides the same policy controls without WPF-UI.

## Validation

A disposable project targeting SnipAgent's exact
`net10.0-windows10.0.19041.0` framework restored `WPF-UI.Markdown` 4.0.2 and
compiled a `WpfRenderer` subclass on Windows on 2026-09-13 with zero warnings
and zero errors.

Implementation validation must additionally prove every security policy and
acceptance scenario in the linked feature specification. Any required use of
the default image/link behavior, global WPF-UI theme dictionaries, or Markdown
preprocessing to enforce security is grounds to reconsider the decision.
