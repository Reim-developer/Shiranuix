package com.reim.shiranuix.service;

import com.reim.shiranuix.model.LibraryEntry;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.Objects;

/**
 * {@link LibraryRepository} that keeps entries in memory only.
 *
 * <p>Entries are deduplicated by folder: opening the same folder again moves
 * it to the top of the list and refreshes its page count rather than adding a
 * duplicate. The list is capped at {@link #MAX_ENTRIES} to keep the home
 * screen from growing unbounded.</p>
 */
public final class InMemoryLibraryRepository implements LibraryRepository {

    /** Maximum number of remembered entries. */
    public static final int MAX_ENTRIES = 24;

    private final List<LibraryEntry> entries = new ArrayList<>();

    @Override
    public List<LibraryEntry> getRecent() {
        return List.copyOf(entries);
    }

    @Override
    public void recordOpen(LibraryEntry entry) {
        Objects.requireNonNull(entry, "entry");
        // Remove any previous entry for the same folder, then re-insert at
        // the front so the most recently opened folder is always first.
        entries.removeIf(existing -> existing.getFolder().equals(entry.getFolder()));
        entries.add(0, entry);

        // Trim the tail if we exceeded the cap.
        while (entries.size() > MAX_ENTRIES) {
            entries.remove(entries.size() - 1);
        }
    }

    @Override
    public boolean remove(Path folder) {
        return entries.removeIf(existing -> existing.getFolder().equals(folder));
    }
}
