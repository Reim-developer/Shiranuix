package com.reim.shiranuix.model;

import java.nio.file.Path;
import java.time.LocalDateTime;
import java.util.Objects;

/**
 * A folder shown on the home screen's recent list.
 *
 * <p>Lightweight summary of a {@link Chapter} suitable for rendering a card
 * without re-scanning the folder. In a later version this will be backed by
 * a SQLite row via {@code SqliteLibraryRepository}.</p>
 */
public final class LibraryEntry {

    private final Path folder;
    private final String title;
    private final int pageCount;
    private final LocalDateTime lastOpened;

    /**
     * Creates a library entry.
     *
     * @param folder     the folder backing this entry
     * @param title      display title (usually the folder name)
     * @param pageCount  number of pages the folder contained when last scanned
     * @param lastOpened when the folder was last opened
     */
    public LibraryEntry(Path folder, String title, int pageCount, LocalDateTime lastOpened) {
        this.folder = Objects.requireNonNull(folder, "folder");
        this.title = Objects.requireNonNull(title, "title");
        if (pageCount < 0) {
            throw new IllegalArgumentException("pageCount must be >= 0, got " + pageCount);
        }
        this.pageCount = pageCount;
        this.lastOpened = Objects.requireNonNull(lastOpened, "lastOpened");
    }

    /**
     * Summarises a scanned chapter as a library entry.
     *
     * @param chapter the chapter to summarise
     * @param opened  when the chapter was opened
     * @return an entry describing the chapter
     */
    public static LibraryEntry from(Chapter chapter, LocalDateTime opened) {
        return new LibraryEntry(
                chapter.folder(), chapter.title(), chapter.getPageCount(), opened);
    }

    /**
     * @return the folder backing this entry
     */
    public Path getFolder() {
        return folder;
    }

    /**
     * @return the display title
     */
    public String getTitle() {
        return title;
    }

    /**
     * @return the number of pages when last scanned
     */
    public int getPageCount() {
        return pageCount;
    }

    /**
     * @return when this folder was last opened
     */
    public LocalDateTime getLastOpened() {
        return lastOpened;
    }

    @Override
    public boolean equals(Object other) {
        if (this == other) {
            return true;
        }
        if (!(other instanceof LibraryEntry that)) {
            return false;
        }
        return folder.equals(that.folder) && lastOpened.equals(that.lastOpened);
    }

    @Override
    public int hashCode() {
        return Objects.hash(folder, lastOpened);
    }

    @Override
    public String toString() {
        return "LibraryEntry[" + title + ", " + pageCount + " pages]";
    }
}
