package com.reim.shiranuix.model;

/**
 * The direction pages are read in.
 *
 * <p>Manga is traditionally read right-to-left, while Western comics are
 * read left-to-right. This axis is deliberately independent of
 * {@link ReadingMode} so that, for example, a webtoon strip can still
 * respect a right-to-left ordering.</p>
 */
public enum ReadingDirection {

    /** Western comics order: first page on the left. */
    LEFT_TO_RIGHT("Left to right"),

    /** Manga order: first page on the right. */
    RIGHT_TO_LEFT("Right to left");

    private final String label;

    ReadingDirection(String label) {
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
