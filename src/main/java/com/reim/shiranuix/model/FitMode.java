package com.reim.shiranuix.model;

/**
 * How a page is scaled to fit the reader viewport.
 *
 * <p>Independent of {@link ReadingMode} and {@link ReadingDirection}, so a
 * user can, for instance, read a double-page spread at "fit width" without
 * affecting the direction or layout settings.</p>
 */
public enum FitMode {

    /** Scale the whole page to fit inside the viewport, preserving aspect ratio. */
    FIT_SCREEN("Fit screen"),

    /** Scale so the page width matches the viewport width. */
    FIT_WIDTH("Fit width"),

    /** Scale so the page height matches the viewport height. */
    FIT_HEIGHT("Fit height"),

    /** Render at the image's native pixel size (no scaling). */
    ORIGINAL_SIZE("Original size");

    private final String label;

    FitMode(String label) {
        this.label = label;
    }

    /**
     * Human-readable label for use in the UI.
     *
     * @return the display label
     */
    public String getLabel() {
        return label;
    }
}
