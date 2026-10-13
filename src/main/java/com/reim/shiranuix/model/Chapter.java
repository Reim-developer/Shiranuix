package com.reim.shiranuix.model;

import java.nio.file.Path;
import java.util.List;
import java.util.Objects;
import java.util.Optional;

/**
 * An ordered collection of {@link MangaPage}s backed by a single folder.
 *
 * <p>Immutable. Pages are stored in reading order (index 1..n). A chapter is
 * produced by {@code FolderScanner}; at the moment one folder equals one
 * chapter, but the abstraction leaves room for multi-folder chapters later.</p>
 */
public record Chapter(Path folder, String title, List<MangaPage> pages) {

    /**
     * Creates a chapter from an ordered list of pages.
     *
     * @param folder the folder the pages were read from
     * @param title  display title (usually the folder name)
     * @param pages  pages in reading order; must not be empty
     */
    public Chapter(Path folder, String title, List<MangaPage> pages) {
        this.folder = Objects.requireNonNull(folder, "folder");
        this.title = Objects.requireNonNull(title, "title");
        Objects.requireNonNull(pages, "pages");
        if (pages.isEmpty()) {
            throw new IllegalArgumentException("A chapter must contain at least one page");
        }
        this.pages = List.copyOf(pages);
    }

    /**
     * @return the folder this chapter was read from
     */
    @Override
    public Path folder() {
        return folder;
    }

    /**
     * @return the display title of the chapter
     */
    @Override
    public String title() {
        return title;
    }

    /**
     * @return an unmodifiable view of the pages, in reading order
     */
    @Override
    public List<MangaPage> pages() {
        return pages;
    }

    /**
     * @return the number of pages in this chapter
     */
    public int getPageCount() {
        return pages.size();
    }

    /**
     * Looks up a page by its 1-based index.
     *
     * @param index 1-based page index
     * @return the page at {@code index}
     * @throws IndexOutOfBoundsException if the index is out of range
     */
    public MangaPage getPage(int index) {
        if (index < 1 || index > pages.size()) {
            throw new IndexOutOfBoundsException(
                    "Page index " + index + " out of range 1.." + pages.size());
        }
        return pages.get(index - 1);
    }

    /**
     * Returns the page at {@code index} if it exists.
     *
     * @param index 1-based page index
     * @return the page, or empty if the index is out of range
     */
    public Optional<MangaPage> findPage(int index) {
        if (index < 1 || index > pages.size()) {
            return Optional.empty();
        }
        return Optional.of(pages.get(index - 1));
    }

    @Override
    public boolean equals(Object other) {
        if (this == other) {
            return true;
        }
        if (!(other instanceof Chapter that)) {
            return false;
        }
        return folder.equals(that.folder) && pages.equals(that.pages);
    }

    @Override
    public int hashCode() {
        return Objects.hash(folder, pages);
    }

    @Override
    public String toString() {
        return "Chapter[" + title + ", " + pages.size() + " pages]";
    }
}
