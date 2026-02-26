# Sentinel UI Style Guide

## Design tokens

### Spacing
| Token   | Value |
|--------|-------|
| SpacingXs  | 4  |
| SpacingSm  | 8  |
| SpacingMd  | 12 |
| SpacingLg  | 16 |
| SpacingXl  | 24 |
| SpacingXxl | 32 |

### Corner radius
| Token              | Value |
|--------------------|-------|
| CornerRadiusCard    | 12 |
| CornerRadiusControl | 10 |
| CornerRadiusPill    | 999 |

### Typography
| Style              | Font size | Weight   | Usage        |
|--------------------|-----------|----------|--------------|
| TitleTextStyle     | 30        | SemiBold | Page titles  |
| SectionTextStyle   | 18        | SemiBold | Section headers |
| BodyTextStyle      | 14        | Normal   | Body         |
| BodySmallTextStyle | 13        | Normal   | Secondary    |
| CaptionTextStyle   | 12        | Normal   | 0.8 opacity  |
| CaptionTinyTextStyle | 11     | Normal   | 0.7 opacity  |

### Brushes
- **CardBackgroundBrush**: `#2A2A2A`
- **CardBorderBrush**: `#404040`
- **Severity**: Ok `#4CAF50`, Warn `#FF9800`, Fail `#F44336`, Info `#2196F3`
- **SubtleTextBrush**: `#B0B0B0`, **MutedBrush**: `#808080`

## Components

- **StatCard**: Value + label + optional SeverityBadge.
- **InsightCard**: Title, explanation, evidence, actions.
- **SeverityBadge**: Ok / Warn / Fail / Info.
- **EmptyState**: Icon + message + optional action.
- **SearchBox**: Debounced input with clear; routes to current ViewModel.
- **SplitPaneHost**: Master–detail (e.g. process list + details pane).
- **SkeletonLoader**: Placeholder while data loads.

## Layout rules

- Use consistent padding (e.g. 24 on page edges).
- Prefer `StackPanel` with `Spacing` for vertical/horizontal stacks.
- Use `ThemeResource ApplicationPageBackgroundThemeBrush` for page background.
- Default theme: Dark; support Light and system in Settings.

## Interaction

- Row hover states on process/service tables.
- Details pane: slide-in animation.
- Page transitions: lightweight opacity/translate.
- Skeleton loading during first data fetch.
