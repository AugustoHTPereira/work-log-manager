import "@testing-library/jest-dom/vitest"

// jsdom does not implement ResizeObserver, which `cmdk` (used by the `Command` shadcn
// component) relies on to measure its list on mount.
if (typeof globalThis.ResizeObserver === "undefined") {
  globalThis.ResizeObserver = class ResizeObserver {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
}

// jsdom also does not implement `scrollIntoView`, called by `cmdk` when the highlighted
// item changes.
if (typeof Element.prototype.scrollIntoView === "undefined") {
  Element.prototype.scrollIntoView = () => {}
}

// jsdom does not implement the pointer capture APIs used by Radix UI's `Select`.
if (typeof Element.prototype.hasPointerCapture === "undefined") {
  Element.prototype.hasPointerCapture = () => false
}
if (typeof Element.prototype.setPointerCapture === "undefined") {
  Element.prototype.setPointerCapture = () => {}
}
if (typeof Element.prototype.releasePointerCapture === "undefined") {
  Element.prototype.releasePointerCapture = () => {}
}
