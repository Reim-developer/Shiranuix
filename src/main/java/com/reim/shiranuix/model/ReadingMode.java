package com.reim.shiranuix.model;

/**
 * Reading layout for the reader.
 *
 * <p>This is one of three independent axes that control how pages are
 * presented (the others are {@link ReadingDirection} and {@link FitMode}).
 * Keeping them separate mirrors the model used by mature readers such as
 * Mihon/Tachiyomi and CDisplayEx, and lets new layouts be added later
 * (e.g. {@code WEBTOON} continuous scroll, {@code DOUBLE_PAGE} spreads)
 * without touching the navigation or fitting logic.</p>
 */
public enum ReadingMode {

    /** One page at a time. The only mode implemented in v1. */
    SINGLE_PAGE("Single page");

    // Future axes (kept commented so the enum stays self-documenting):
    // WEBTOON("Webtoon / long strip"),
    // DOUBLE_PAGE("Double page");

    private final String label;

    ReadingMode(String label) {
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
