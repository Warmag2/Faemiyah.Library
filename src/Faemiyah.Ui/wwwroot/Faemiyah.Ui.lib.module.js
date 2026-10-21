// Faemiyah UI browser support. Blazor loads this module automatically as a JavaScript initializer.
// The listeners are delegated from the document, so they work for components rendered at any time.

let installed = false;

function installTooltips() {
    // Elements carry data-fui-tooltip="<tooltip element id>" and either
    // data-fui-tooltip-text (shown as plain text) or data-fui-tooltip-html (shown as HTML).
    // Only use data-fui-tooltip-html with trusted or encoded content.
    const targetSelector = "[data-fui-tooltip]";
    let activeTooltip = null;
    let activeTarget = null;
    let activeContent = null;

    function findTarget(evt) {
        return evt.target instanceof Element ? evt.target.closest(targetSelector) : null;
    }

    function readContent(target) {
        if (target.hasAttribute("data-fui-tooltip-html")) {
            return { html: true, value: target.getAttribute("data-fui-tooltip-html") ?? "" };
        }

        return { html: false, value: target.getAttribute("data-fui-tooltip-text") ?? "" };
    }

    function write(tooltip, content) {
        if (content.html) {
            tooltip.innerHTML = content.value;
        } else {
            tooltip.textContent = content.value;
        }
    }

    function sameContent(a, b) {
        return a !== null && b !== null && a.html === b.html && a.value === b.value;
    }

    function position(tooltip, evt) {
        tooltip.style.left = evt.pageX + 10 + "px";
        tooltip.style.top = evt.pageY + 10 + "px";
    }

    document.addEventListener("mouseover", function (evt) {
        const target = findTarget(evt);
        if (target === null) {
            return;
        }

        const tooltip = document.getElementById(target.getAttribute("data-fui-tooltip"));
        if (tooltip === null) {
            return;
        }

        activeTarget = target;
        activeTooltip = tooltip;
        activeContent = readContent(target);
        write(tooltip, activeContent);
        tooltip.style.display = "block";
        position(tooltip, evt);
    });

    document.addEventListener("mousemove", function (evt) {
        if (activeTooltip === null) {
            return;
        }

        // The content may change while hovering, for example after a re-render.
        const content = readContent(activeTarget);
        if (!sameContent(content, activeContent)) {
            activeContent = content;
            write(activeTooltip, content);
        }

        position(activeTooltip, evt);
    });

    document.addEventListener("mouseout", function (evt) {
        const target = findTarget(evt);
        if (target === null) {
            return;
        }

        if (evt.relatedTarget instanceof Node && target.contains(evt.relatedTarget)) {
            return;
        }

        const tooltip = document.getElementById(target.getAttribute("data-fui-tooltip"));
        if (tooltip !== null) {
            tooltip.style.display = "none";
        }

        if (target === activeTarget) {
            activeTooltip = null;
            activeTarget = null;
            activeContent = null;
        }
    });
}

function installReorderableLists() {
    // Drag-and-drop highlighting for ContainerReorderableList is handled here, without .NET round trips.
    // Only dragstart, drop and dragend reach .NET. One active container is tracked so nested lists do not interfere.
    const containerSelector = ".fui-reorderable-list";
    const itemClass = "fui-reorderable-item";
    const targetSelector = ".fui-reorderable-item, .fui-reorderable-sentinel";
    const sourceSelector = ".fui-reorderable-item, .fui-drag-handle";
    const draggingClass = "fui-reorderable-list--dragging";
    const itemOverClass = "fui-reorderable-item--dropover";
    const sentinelOverClass = "fui-reorderable-sentinel--dropover";

    let activeContainer = null;

    function clearDropOver(container) {
        if (container === null) {
            return;
        }

        const marked = container.querySelectorAll("." + itemOverClass + ", ." + sentinelOverClass);
        for (const element of marked) {
            // Leave elements of nested lists alone.
            if (element.closest(containerSelector) === container) {
                element.classList.remove(itemOverClass, sentinelOverClass);
            }
        }
    }

    function endDrag() {
        if (activeContainer === null) {
            return;
        }

        activeContainer.classList.remove(draggingClass);
        clearDropOver(activeContainer);
        activeContainer = null;
    }

    document.addEventListener("dragstart", function (evt) {
        if (!(evt.target instanceof Element)) {
            return;
        }

        const source = evt.target.closest(sourceSelector);
        if (source === null) {
            return;
        }

        const container = source.closest(containerSelector);
        if (container === null) {
            return;
        }

        endDrag();
        activeContainer = container;
        container.classList.add(draggingClass);
    }, true);

    document.addEventListener("dragenter", function (evt) {
        if (activeContainer === null || !(evt.target instanceof Element)) {
            return;
        }

        clearDropOver(activeContainer);

        const over = evt.target.closest(targetSelector);
        if (over === null || over.closest(containerSelector) !== activeContainer) {
            return;
        }

        over.classList.add(over.classList.contains(itemClass) ? itemOverClass : sentinelOverClass);
    }, true);

    document.addEventListener("drop", endDrag, true);
    document.addEventListener("dragend", endDrag, true);
}

function install() {
    if (installed || typeof document === "undefined") {
        return;
    }

    installed = true;
    installTooltips();
    installReorderableLists();
}

// Blazor Server and WebAssembly standalone apps.
export function afterStarted() {
    install();
}

// Blazor Web Apps.
export function afterWebStarted() {
    install();
}
