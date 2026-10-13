package com.reim.shiranuix.model;

import java.nio.file.Path;
import java.util.Objects;

/**
 * A single page of a chapter, backed by one image file on disk.
 *
 * <p>Immutable value object. The {@code index} is 1-based to match what the
 * UI shows to the user (page 1, page 2, ...).</p>
 */
public final class MangaPage {

    private final Path imageFile;
    private final int index;

    /**
     * Creates a page.
     *
     * @param imageFile path to the backing image file
     * @param index     1-based position of this page within its chapter
     */
    public MangaPage(Path imageFile, int index) {
        this.imageFile = Objects.requireNonNull(imageFile, "imageFile");
        if (index < 1) {
            throw new IllegalArgumentException("Page index must be >= 1, got " + index);
        }
        this.index = index;
    }

    /**
     * @return path to the image file backing this page
     */
    public Path getImageFile() {
        return imageFile;
    }

    /**
     * @return 1-based position of this page within its chapter
     */
    public int getIndex() {
        return index;
    }

    /**
     * @return the file name of the backing image, for display purposes
     */
    public String getFileName() {
        Path fileName = imageFile.getFileName();
        return fileName == null ? imageFile.toString() : fileName.toString();
    }

    @Override
    public boolean equals(Object other) {
        if (this == other) {
            return true;
        }
        if (!(other instanceof MangaPage that)) {
            return false;
        }
        return index == that.index && imageFile.equals(that.imageFile);
    }

    @Override
    public int hashCode() {
        return Objects.hash(imageFile, index);
    }

    @Override
    public String toString() {
        return "MangaPage[" + index + ": " + getFileName() + "]";
    }
}
