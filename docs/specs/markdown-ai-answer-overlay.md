# Markdown rendering in the AI answer overlay

**Status:** Implementing
**Owner:** Repository owner
**Last reviewed:** 2026-09-13

## Problem

The AI answer overlay displays the completed AI response through a plain WPF
`TextBlock`. Markdown markers such as headings, lists, emphasis, tables, and
code fences therefore appear as raw text instead of a formatted answer. This
makes structured responses harder to scan and code-heavy responses harder to
read.

## Desired outcome

Users see completed AI answers as readable, themed Markdown in the existing
overlay while retaining its current sizing, scrolling, movement, opacity,
keyboard, cancellation, and save behavior. Untrusted answer content must not
silently execute embedded HTML, open external resources, or load remote images.

## In scope

- Render completed, non-empty AI answers as Markdown in
  `AiAnswerOverlayWindow`.
- Support paragraphs, line breaks, headings, bold and italic text, inline code,
  fenced code blocks, block quotes, ordered and unordered lists, task lists,
  thematic breaks, links, and tables.
- Style rendered content for the overlay's existing dark presentation,
  including readable heading hierarchy, list indentation, table borders, block
  quotes, inline code, and horizontally scrollable code blocks.
- Keep the rendered answer vertically scrollable and responsive to overlay
  resizing.
- Keep tables wider than the answer viewport inside their own horizontal
  scrolling area so the rest of the answer does not scroll horizontally.
- Allow users to select and copy rendered text with standard WPF keyboard and
  context-menu behavior.
- Treat the AI response as untrusted input: do not interpret embedded HTML, do
  not load remote images, and do not navigate non-HTTP(S) links.
- Present raw HTML as literal, inert source text.
- Replace each blocked Markdown image with a non-interactive indicator that
  includes the image's alt text.
- Open an allowed HTTP(S) link only after the user activates it, using the
  Windows default browser.
- Preserve the original response string in `AnswerResult` so file-saving
  behavior continues to store the model's Markdown rather than text extracted
  back from the rendered document.
- Preserve existing ready, running, empty-answer, cancellation, warning, and
  failure messages as readable plain text.
- Fall back to a plain-text presentation with an explicit rendering warning if
  the Markdown renderer rejects an otherwise valid AI response.
- Add focused automated coverage for Markdown-to-view behavior that can run
  without an interactive window, plus manual Windows validation of the
  rendered WPF control.
- Update current-behavior documentation and both user manuals.

## Out of scope

- Streaming or incrementally rendering a response while the model is running;
  the current answer flow supplies the UI only after the complete response is
  available.
- Rendering Markdown in AI extraction files, notifications, settings, skill
  editing, or any window other than the AI answer overlay.
- Editing Markdown, showing a source/preview toggle, or adding a dedicated
  "copy raw Markdown" command.
- Executing or visually interpreting raw HTML, scripts, embedded web content,
  Mermaid diagrams, mathematical notation, or arbitrary XAML.
- Loading remote, local-file, data-URI, or package-resource images from AI
  output.
- Syntax highlighting, code execution, or a code-block copy button in the first
  release.
- Changing AI prompts to force a particular Markdown dialect.
- Changing overlay dimensions, remembered settings, movement, opacity,
  dismissal, request cancellation, or result-saving semantics.
- Migrating the WPF application to WinUI or Windows App SDK.

## Acceptance scenarios

1. **Given** an AI response containing headings, paragraphs, emphasis, inline
   code, fenced code, block quotes, lists, task lists, a thematic break, and a
   table
   **When** the request completes
   **Then** the overlay presents each supported construct with a distinct,
   readable WPF visual treatment instead of showing its Markdown delimiters.

2. **Given** a completed answer is longer or wider than its viewport
   **When** the user scrolls or resizes the overlay
   **Then** the answer remains vertically scrollable, content reflows to the
   available width, and code blocks and wide tables scroll horizontally within
   their own areas without forcing the whole answer viewport wider.

3. **Given** the rendered answer contains text in paragraphs, lists, tables, or
   code blocks
   **When** the user selects content and invokes **Copy** or **Ctrl+C**
   **Then** the selected readable text is placed on the clipboard using standard
   WPF behavior.

4. **Given** an answer contains an absolute `https://` or `http://` Markdown
   link
   **When** the user activates that link
   **Then** the URI is opened through the Windows default browser and no
   navigation occurs merely because the answer was rendered.

5. **Given** an answer contains a relative link or a URI with any scheme other
   than HTTP or HTTPS
   **When** the answer is rendered or the user activates the link
   **Then** the label remains readable but the application does not navigate to
   the target or invoke the Windows shell for it.

6. **Given** an answer contains Markdown images referencing remote, local,
   package, or data URIs
   **When** the answer is rendered
   **Then** the application performs no image retrieval or file access and
   displays a non-interactive blocked-image indicator containing the image's
   alt text.

7. **Given** an answer contains raw HTML, script, iframe, object, or embedded
   XAML-like content
   **When** the answer is rendered
   **Then** no embedded content executes or creates interactive UI, and the
   raw HTML source is shown as literal, inert text.

8. **Given** an answer is malformed or uses unsupported Markdown
   **When** it is rendered
   **Then** all recoverable source text remains readable and the overlay remains
   responsive.

9. **Given** Markdown conversion throws an error
   **When** the completed answer is presented
   **Then** the overlay visibly reports that formatting failed, displays the
   original answer as plain text, and retains that original string for the
   existing save flow.

10. **Given** the request is ready, running, canceled, failed, or returns only
    whitespace
    **When** the overlay updates its status
    **Then** its existing messages and Enter, Esc, close-button, and cancellation
    behavior remain unchanged.

11. **Given** the same completed answer is displayed and file saving is enabled
    **When** the user closes the overlay
    **Then** the saved `.md` result contains the original model response with no
    presentation-only transformations.

12. **Given** Windows text scaling, keyboard navigation, or a supported
    per-monitor DPI transition is in use
    **When** the rendered answer is viewed and operated
    **Then** text remains legible, link focus is visible, selection and scrolling
    remain usable, and existing overlay positioning and resizing behavior does
    not regress.

## Constraints and assumptions

- Verified current behavior: the application targets
  `net10.0-windows10.0.19041.0` with WPF, and the overlay assigns the completed
  response directly to a `TextBlock` only after `AnswerAsync` returns.
- Verified current behavior: `AnswerResult` retains the original answer string
  for the post-overlay save flow.
- The response is untrusted even when it came from a configured model because
  captured content, hosted search, or MCP results can influence it.
- Markdown parsing and rendering must stay local. A browser control, hosted web
  renderer, and conversion service are not acceptable for this feature.
- The selected renderer must be compatible with .NET 10 WPF and must not change
  application-wide theme resources as a side effect.
- Rendering must run on the WPF dispatcher because the output is a WPF document
  or visual tree. The implementation must avoid blocking work that is
  proportional to network access; external images are prohibited.
- Confirmed by the repository owner on 2026-09-13: the first release supports
  common Markdown plus tables and task lists; opens only user-activated HTTP(S)
  links; blocks all image loading; shows blocked-image alt text with an
  indicator; displays raw HTML as literal text; provides standard selection and
  rendered-text copying without a raw-Markdown copy command; and gives each
  wide table its own horizontal scrolling area.

## Renderer research

Research was performed against package metadata, project repositories, and
Microsoft's WPF rich-text documentation on 2026-09-13.

### Platform finding

WPF has native `FlowDocument`, `FlowDocumentScrollViewer`, and `RichTextBox`
surfaces for formatted paragraphs, lists, tables, images, and selection, but it
does not include a Markdown parser. Windows App SDK's `RichTextBlock` is not a
drop-in option for this WPF application and also does not parse Markdown.

### Package comparison

| Candidate | Findings | Assessment |
| --- | --- | --- |
| [MdXaml](https://www.nuget.org/packages/MdXaml/) 1.27.0 | MIT; native WPF `FlowDocument`; repository is active and not archived; supports .NET 8 WPF and .NET Framework; includes selection/copy and tables; mature history and materially higher adoption than the newer candidates. Its built-in viewer also has link-opening and asset-loading capabilities that must be explicitly constrained. | Leading mature candidate if a proof confirms .NET 10 compatibility, dark-theme control, inert HTML, and complete suppression of image loading. |
| [MarkdownViewer.Wpf](https://www.nuget.org/packages/MarkdownViewer.Wpf/) 2.0.4368 | MIT; native WPF visual tree; targets .NET 10 WPF directly; Markdig-based; supports the requested syntax and explicit injectable link-navigation and image-resolution services. It was created in 2026, has very low adoption, and includes HTML rendering and image/link defaults that must be replaced or disabled. | Best API fit for the security requirements, but maturity and dependency risk require a proof and review before adoption. |
| [WPF-UI.Markdown](https://www.nuget.org/packages/WPF-UI.Markdown/) 4.0.2 | MIT; active; native WPF; Markdig and ColorCode based; supports tables, task lists, images, links, and code highlighting. It additionally depends on WPF-UI and expects its theme dictionaries, while SnipAgent does not otherwise use WPF-UI. | Not preferred because it introduces a broader UI framework and application resource integration for one overlay. |
| [Markdig.Wpf](https://www.nuget.org/packages/Markdig.Wpf/) 0.5.0.1 | MIT; native WPF `FlowDocument`; CommonMark/Markdig feature set. The repository is archived, identifies itself as unmaintained, targets only through .NET 5 WPF, and pins an old Markdig version. | Reject for new production use. |
| WebView/browser-based renderers | Can provide broad HTML/CSS compatibility and syntax highlighting. They add a browser runtime, HTML sanitization boundary, navigation/network controls, focus interoperability, and a heavier startup/resource profile. | Reject because the feature needs a small, native, offline WPF answer surface rather than an embedded web security boundary. |

### Decision outcome

The repository owner selected **WPF-UI.Markdown 4.0.2 with an
application-owned safe renderer subclass**. Although this introduces broader
dependencies than MdXaml, it produces selectable `FlowDocument` content and
provides public renderer extension points that can enforce the approved link,
image, HTML, code-block, and table policies without a maintained fork.

A disposable project targeting SnipAgent's exact
`net10.0-windows10.0.19041.0` framework restored the pinned package and compiled
a `WpfRenderer` subclass on Windows with zero warnings and zero errors. The
spike also found that the shipped package lacks a viewer property present in
the current repository source, so implementation must compile against and
validate the pinned NuGet API rather than relying on unreleased source.

The accepted decision and its consequences are recorded in
[ADR 0001](../adr/0001-render-markdown-with-wpf-ui-markdown.md).

## Implementation outline

Introduce a small application-owned Markdown presentation boundary around the
pinned WPF-UI.Markdown parser and renderer infrastructure rather than assigning
the package's default control throughout the overlay. It should accept the raw
answer, produce a themed `FlowDocument`, register only application-owned safe
renderers for security-sensitive elements, and report conversion failures
explicitly. Keep status and operational messages on the existing plain-text
path.

Replace only the completed-answer viewport with the presentation boundary.
Continue assigning `AnswerResult` before rendering so a presentation failure
cannot discard or rewrite the answer. Apply overlay-local styles and accessible
link/placeholder labels without merging renderer theme resources globally.

Keep parsing-policy tests independent of an interactive window where possible.
Use manual Windows validation for selection, clipboard behavior, keyboard focus,
browser launch, scrolling, resize/DPI behavior, and visual contrast.

## Tasks

- [x] Confirm supported syntax, link behavior, blocked-image presentation, raw
  HTML presentation, wide-table overflow, and rendered-text copy behavior with
  the repository owner.
- [x] Verify the selected WPF-UI.Markdown package restores against the exact
  application target and supports an application-owned renderer subclass.
- [x] Record the selected renderer, rejected alternatives, security controls,
  and package-version policy in an ADR.
- [x] Add an application-owned Markdown presentation boundary with explicit
  link, image, HTML, and error policies.
- [x] Render completed answers through that boundary while preserving the raw
  `AnswerResult` and all non-completed status paths.
- [x] Add overlay-local dark styles, selection/copy behavior, accessible link
  focus, responsive tables, and independently scrolling code blocks.
- [x] Add focused tests for supported syntax, blocked external content,
  malformed input, fallback behavior, and preservation of the raw result.
- [ ] Manually validate visual quality, copying, allowed and blocked links,
  image blocking, resizing, text scaling, and mixed-DPI behavior on Windows.
- [x] Update `FEATURES_AND_DEVELOPMENT.md`, `USER_MANUAL.md`, and
  `USER_MANUAL_KR.md`.
- [ ] Run `.\scripts\verify.ps1`.

## Verification evidence

### Cloud or Ubuntu

Command: `bash ./scripts/verify-ubuntu.sh`

Environment: Ubuntu cloud-agent workspace,
`/home/runner/work/win-snipagent/win-snipagent`, .NET SDK `10.0.401`.

Result: restore and compile completed successfully for `SnipAgent.App` and
`SnipAgent.Tests` with `0 Warning(s)` and `0 Error(s)`.

Limitations: compile-only evidence. This command does not execute Windows
runtime tests or interactive WPF validation.

### Windows automated

Pending on Windows:

- Focused renderer tests (`AiAnswerMarkdownPresenterTests`) on a Windows runtime.
- Final repository verification command: `.\scripts\verify.ps1`.

### Manual Windows

Pending on Windows (manual):

- Supported Markdown rendering quality (headings, paragraphs, emphasis, inline
  code, fenced code, block quotes, ordered/unordered lists, task lists, thematic
  breaks, links, and tables).
- Selection and copy behavior (`Copy` and `Ctrl+C`) across rendered content.
- Allowed link activation (user-activated absolute HTTP(S) only).
- Blocked links (relative/non-HTTP(S) remain readable and inert).
- Image blocking for remote/local/package/data URIs with alt-text indicators.
- Raw HTML/script/iframe/object/XAML-like content remains literal and inert.
- Explicit plain-text fallback and warning path if rendering fails.
- Overflow behavior (local horizontal scroll for long code/wide tables), resize,
  text scaling, keyboard focus visibility, and same-/mixed-DPI behavior.

## Related decisions

- [ADR 0001: Render AI answers with WPF-UI.Markdown](../adr/0001-render-markdown-with-wpf-ui-markdown.md)
