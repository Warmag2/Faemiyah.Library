# Faemiyah UI

Generic Blazor input and container components with their own stylesheet. Extracted from BtDamageResolver.

Works with Blazor Server, Blazor WebAssembly and Blazor Web Apps on .NET 10.

## Usage

1. Reference the project or the `Faemiyah.Ui` package.
2. Add the namespaces to `_Imports.razor`:

   ```razor
   @using Faemiyah.Ui
   @using Faemiyah.Ui.Components
   ```

3. Link the stylesheet in the host page (`App.razor` or `index.html`):

   ```html
   <link rel="stylesheet" href="_content/Faemiyah.Ui/faemiyah-ui.css" />
   ```

The JavaScript (`Faemiyah.Ui.lib.module.js`) is a Blazor JavaScript initializer and is loaded automatically. It provides the tooltip and the drag-and-drop highlighting.

## Components

| Component | Purpose |
|---|---|
| `ContainerAccordion` | Collapsible section with a header button. |
| `ContainerExpander` | Switches between two contents with a corner button. |
| `ContainerModal` | Modal dialog. The footer with primary and cancel buttons appears when `OnPrimary` is set. |
| `ContainerReorderableList<TItem>` | Drag-and-drop reordering of an `IList<TItem>`, in place. Optional drag handle. |
| `ContainerTabSelector`, `ContainerTab` | Tab row and tab content. The parent owns the selected tab. |
| `FormCheckbox` | Styled checkbox. |
| `FormToggle` | Checkbox styled as a toggle button. |
| `FormRadio<TItem>` | Radio group styled as toggle buttons. |
| `FormSelect<TItem>` | Drop-down select. |
| `FormComboBox<TItem>` | Text input with a filtered option list. Only option keys are accepted. |
| `FormText`, `FormTextArea` | Text inputs, optionally rejecting values from `InvalidOptionGenerator`. |
| `FormNumber` | Number input which commits on blur, Enter or Escape and clamps to `[Min, Max]`. |
| `FormNumberPicker` | `FormNumber` with a drop-down of `PickBracket`s. |
| `FormNumberPickerDisplayOnly` | Bold number with a bracket drop-down; click the number to type a value. |
| `FormPickSet<TItem>` | Editable set of items, added through a combo box. |
| `Tooltip` | Tooltip element for the `data-fui-tooltip` attributes. |

Option-based components take dictionaries keyed by display text.

Most inputs show the `fui-reminder` class while their value is older than `ComparisonTimeStamp`.

### Behaviour to be aware of

- Except for `FormNumber` and `FormRadio`, inputs read their value parameter only once, in `OnInitialized`. Later changes from the parent are ignored. Re-create the component (for example with `@key`) to reset it.
- `FormCheckbox`, `FormToggle`, `FormSelect`, `FormText`, `FormTextArea`, `FormNumberPicker` and `FormNumberPickerDisplayOnly` invoke `OnChanged` once during initialization with the initial value.
- `FormCheckbox` counts its initialization as an update. Its reminder class therefore only appears when `ComparisonTimeStamp` is later than the moment the component was created.

## Tooltips

Place one `Tooltip` component on the page and mark the target elements:

```razor
<Tooltip Id="tooltip" />

<span data-fui-tooltip="tooltip" data-fui-tooltip-text="@user.Name">...</span>
<span data-fui-tooltip="tooltip" data-fui-tooltip-html="@trustedMarkup">...</span>
```

`data-fui-tooltip-text` is shown as plain text, and line breaks are kept. Prefer it for anything containing user input.

`data-fui-tooltip-html` is inserted as HTML. Only use it with trusted or HTML-encoded content, or it becomes a script injection hole.

## Theming

All colours, the control font and the z-indexes are CSS variables defined on `:root` in `faemiyah-ui.css`, for example `--fui-color-bg-selected`, `--fui-color-bg-hover`, `--fui-font-family-control` and `--fui-z-modal`. Override them in your own stylesheet after the library stylesheet.

Shared state classes are `fui-active`, `fui-inactive`, `fui-error`, `fui-reminder` and `fui-disabled`. `FuiStyle` exposes them and the `HideElement` and `GetActiveClass` helpers.

Drag handles are hidden below a viewport width of `50rem`.

## Migrating from BtDamageResolver

The component names and parameters are unchanged, with these additions:

- `ContainerModal.CancelText` (default `Cancel`)
- `FormTextArea.Rows` (default `10`)
- `Tooltip` component

All buttons are `type="button"`, so they no longer submit forms.

Parameter type changes:

- `ContainerTabSelector.TabNames` is an `IReadOnlyList<string>`.
- `BracketCreatorDelegate` returns `IReadOnlyList<PickBracket>`.
- `InvalidOptionGenerator` returns `IEnumerable<string>`.

Existing lambdas returning lists still compile. `PickBracket` now lives in `Faemiyah.Ui`.

Class and attribute renames:

| Old | New |
|---|---|
| `resolver_input`, `resolver_input_text`, `resolver_input_number`, `resolver_input_select` | `fui-input`, `fui-input-text`, `fui-input-number`, `fui-input-select` |
| `fullwidth`, `error`, `reminder`, `active`, `inactive`, `disabled` | `fui-fullwidth`, `fui-error`, `fui-reminder`, `fui-active`, `fui-inactive`, `fui-disabled` |
| `resolver_button`, `resolver_button_add`, `resolver_button_delete` | `fui-button`, `fui-button-add`, `fui-button-delete` |
| `resolver_button_accordion`, `resolver_accordion_indicator`, `resolver_div_accordioncontent` | `fui-accordion-button`, `fui-accordion-indicator`, `fui-accordion-content` |
| `resolver_div_componentcontainer` (expander), `resolver_button_expander` | `fui-expander`, `fui-expander-button` |
| `resolver_modal_background`, `resolver_modal`, `resolver_modal_header`, `resolver_modal_body`, `resolver_modal_footer` | `fui-modal-background`, `fui-modal`, `fui-modal-header`, `fui-modal-body`, `fui-modal-footer` |
| `resolver_header` (modal title), `button_modal_close` | `fui-modal-title`, `fui-modal-close` |
| `resolver_div_tabcontainer`, `resolver_div_tab` | `fui-tabs`, `fui-tab` |
| `resolver_label_checkbox`, `checkmark` | `fui-checkbox`, `fui-checkmark` |
| `resolver_div_radiogroup`, `resolver_label_toggleradio`, `resolver_span_toggleradio` | `fui-radio-group`, `fui-toggle-radio`, `fui-toggle-radio-label` |
| `resolver_div_inputwrapper`, `combobox` | `fui-input-wrapper`, `fui-combobox` |
| `resolver_div_combobox`, `resolver_div_comboboxitem` | `fui-combobox-list`, `fui-combobox-item` |
| `resolver_div_picker`, `resolver_div_pickeritem`, `resolver_number_picker_display_only` | `fui-picker`, `fui-picker-item`, `fui-number-display` |
| `resolver_div_pickset`, `resolver_div_unitinformation_data` (pick set item) | `fui-pickset`, `fui-pickset-item` |
| `resolver_div_reorderablelistcontainer`, `resolver_div_reorderableitem`, `draggable`, `resolver_div_reorderablesentinel` | `fui-reorderable-list`, `fui-reorderable-item`, `fui-draggable`, `fui-reorderable-sentinel` |
| `resolver_div_draghandle`, `resolver_div_componentrow`, `resolver_div_componentcell` (in the list) | `fui-drag-handle`, `fui-reorderable-row`, `fui-reorderable-cell` |
| `--dragging`, `--dropover` suffixes | same suffixes on the new names |
| `resolver_div_tooltip` | `fui-tooltip` (or the `Tooltip` component) |
| `data-tooltip-id`, `data-tooltip-content` (HTML) | `data-fui-tooltip`, `data-fui-tooltip-html`; or `data-fui-tooltip-text` for plain text |
| `--color_*` variables | `--fui-color-*` variables |

## Development

```powershell
dotnet build Faemiyah.Ui.slnx
dotnet test Faemiyah.Ui.slnx
```

Building packs the NuGet package (`GeneratePackageOnBuild`).

## License

BSD-3-Clause. See [LICENSE](LICENSE).
