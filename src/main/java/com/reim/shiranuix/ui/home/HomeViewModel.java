package com.reim.shiranuix.ui.home;

import com.reim.shiranuix.model.Chapter;
import com.reim.shiranuix.model.LibraryEntry;
import com.reim.shiranuix.service.FolderScanner;
import com.reim.shiranuix.service.LibraryRepository;
import java.nio.file.Path;
import java.time.LocalDateTime;
import java.util.List;
import java.util.Objects;
import java.util.Optional;
import java.util.function.Consumer;
import javafx.beans.property.ReadOnlyListProperty;
import javafx.beans.property.ReadOnlyListWrapper;
import javafx.beans.property.SimpleStringProperty;
import javafx.beans.property.StringProperty;
import javafx.collections.FXCollections;

/**
 * State and behaviour for the home (recent folders) screen.
 *
 * <p>Contains no JavaFX UI types, so it can be unit-tested headlessly. The
 * view observes the exposed read-only properties and forwards user actions
 * to the command methods here.</p>
 */
public final class HomeViewModel {

    private final FolderScanner scanner;
    private final LibraryRepository repository;

    private final ReadOnlyListWrapper<LibraryEntry> recent =
            new ReadOnlyListWrapper<>(FXCollections.observableArrayList());

    private final StringProperty status = new SimpleStringProperty("");

    /** Callback invoked when the user opens a folder and it is ready to read. */
    private Consumer<Chapter> openHandler = chapter -> { };

    /**
     * Creates a home view model.
     *
     * @param scanner   the folder scanner used to turn a folder into a chapter
     * @param repository the store backing the recent list
     */
    public HomeViewModel(FolderScanner scanner, LibraryRepository repository) {
        this.scanner = Objects.requireNonNull(scanner, "scanner");
        this.repository = Objects.requireNonNull(repository, "repository");
        refreshRecent();
    }

    /**
     * @return read-only view of the recent entries, most recent first
     */
    public ReadOnlyListProperty<LibraryEntry> recentProperty() {
        return recent.getReadOnlyProperty();
    }

    /**
     * @return the current entries, most recent first
     */
    public List<LibraryEntry> getRecent() {
        return List.copyOf(recent);
    }

    /**
     * @return read-only status line shown under the header
     */
    public StringProperty statusProperty() {
        return status;
    }

    /**
     * Registers the callback fired when a folder is successfully opened.
     *
     * @param handler receives the scanned chapter
     */
    public void setOpenHandler(Consumer<Chapter> handler) {
        this.openHandler = Objects.requireNonNull(handler, "handler");
    }

    /**
     * Scans {@code folder} and, on success, records it and notifies the
     * open handler.
     *
     * <p>On failure the status message is updated and the handler is not
     * called, so the user stays on the home screen.</p>
     *
     * @param folder the folder the user chose
     */
    public void openFolder(Path folder) {
        if (folder == null) {
            status.set("No folder selected.");
            return;
        }

        Optional<Chapter> result = scanner.scan(folder);
        if (result.isEmpty()) {
            status.set("No images found in " + folder.getFileName() + ".");
            return;
        }

        Chapter chapter = result.get();
        repository.recordOpen(LibraryEntry.from(chapter, LocalDateTime.now()));
        refreshRecent();
        status.set("Opened " + chapter.title()
                + " (" + chapter.getPageCount() + " pages).");
        openHandler.accept(chapter);
    }

    /** Re-reads the recent list from the repository. */
    private void refreshRecent() {
        recent.setAll(repository.getRecent());
    }
}
