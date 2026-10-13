package com.reim.shiranuix.service;

import com.reim.shiranuix.model.LibraryEntry;
import java.util.List;

/**
 * Stores the set of folders the user has opened (the "recent" list shown on
 * the home screen).
 *
 * <p>This is the seam where persistence plugs in. The in-memory
 * implementation keeps everything in a list for the current session; a
 * later {@code SqliteLibraryRepository} will back the same interface with a
 * database file so the list survives restarts. The UI does not change when
 * that happens.</p>
 */
public interface LibraryRepository {

    /**
     * @return the known entries, most recently opened first
     */
    List<LibraryEntry> getRecent();

    /**
     * Records that a folder was just opened, moving it to the top of the
     * recent list.
     *
     * @param entry the entry to record
     */
    void recordOpen(LibraryEntry entry);

    /**
     * Removes an entry by its folder.
     *
     * @param folder the folder whose entry should be removed
     * @return {@code true} if an entry was removed
     */
    boolean remove(java.nio.file.Path folder);
}
