package com.reim.shiranuix.ui.reader;

import com.reim.shiranuix.model.Chapter;
import com.reim.shiranuix.model.FitMode;
import com.reim.shiranuix.model.MangaPage;
import com.reim.shiranuix.model.ReadingDirection;
import com.reim.shiranuix.model.ReadingMode;
import com.reim.shiranuix.service.ImageLoader;
import java.nio.file.Path;
import java.util.Objects;
import java.util.Optional;
import java.util.function.Consumer;
import javafx.beans.property.BooleanProperty;
import javafx.beans.property.ObjectProperty;
import javafx.beans.property.ReadOnlyBooleanProperty;
import javafx.beans.property.ReadOnlyBooleanWrapper;
import javafx.beans.property.ReadOnlyIntegerProperty;
import javafx.beans.property.ReadOnlyIntegerWrapper;
import javafx.beans.property.ReadOnlyObjectProperty;
import javafx.beans.property.ReadOnlyObjectWrapper;
import javafx.beans.property.SimpleBooleanProperty;
import javafx.beans.property.SimpleObjectProperty;
import javafx.scene.image.Image;

/**
 * State and behaviour for the reader screen.
 *
 * <p>This view model owns the reading position and the three independent
 * presentation axes ({@link ReadingMode}, {@link ReadingDirection},
 * {@link FitMode}). It depends on {@link ImageLoader} only through its
 * interface, so tests can substitute a fake loader and exercise navigation
 * without decoding real images.</p>
 *
 * <p>No JavaFX control or node types appear here, keeping the class
 * headlessly testable.</p>
 */
public final class ReaderViewModel {

    private final ImageLoader imageLoader;

    private final ReadOnlyObjectWrapper<Chapter> chapter =
            new ReadOnlyObjectWrapper<>();
    private final ReadOnlyIntegerWrapper currentPageIndex =
            new ReadOnlyIntegerWrapper(this, "currentPageIndex", 1);
    private final ReadOnlyIntegerWrapper pageCount =
            new ReadOnlyIntegerWrapper(this, "pageCount", 0);
    private final ReadOnlyObjectWrapper<Image> currentImage =
            new ReadOnlyObjectWrapper<>(this, "currentImage");

    private final ObjectProperty<ReadingMode> readingMode =
            new SimpleObjectProperty<>(ReadingMode.SINGLE_PAGE);
    private final ObjectProperty<ReadingDirection> readingDirection =
            new SimpleObjectProperty<>(ReadingDirection.RIGHT_TO_LEFT);
    private final ObjectProperty<FitMode> fitMode =
            new SimpleObjectProperty<>(FitMode.FIT_SCREEN);

    private final ReadOnlyBooleanWrapper canGoNext =
            new ReadOnlyBooleanWrapper(this, "canGoNext", false);
    private final ReadOnlyBooleanWrapper canGoPrevious =
            new ReadOnlyBooleanWrapper(this, "canGoPrevious", false);

    /** Callback invoked when the user asks to leave the reader. */
    private Runnable backHandler = () -> { };

    /** Receives a user-facing message (e.g. "end of chapter"). */
    private Consumer<String> messageHandler = message -> { };

    /** Guards against a stale async load overwriting a newer page. */
    private int loadToken;

    /**
     * Creates a reader view model.
     *
     * @param imageLoader the loader used to decode page images
     */
    public ReaderViewModel(ImageLoader imageLoader) {
        this.imageLoader = Objects.requireNonNull(imageLoader, "imageLoader");

        // Keep the derived state in sync whenever the position or chapter changes.
        currentPageIndex.addListener((obs, old, value) -> refreshDerivedState());
        chapter.addListener((obs, old, value) -> refreshDerivedState());
    }

    // ------------------------------------------------------------------
    // Read-only state exposed to the view
    // ------------------------------------------------------------------

    /** @return the chapter currently being read, or {@code null} if none */
    public ReadOnlyObjectProperty<Chapter> chapterProperty() {
        return chapter.getReadOnlyProperty();
    }

    /** @return 1-based index of the page currently displayed */
    public ReadOnlyIntegerProperty currentPageIndexProperty() {
        return currentPageIndex.getReadOnlyProperty();
    }

    /** @return number of pages in the current chapter */
    public ReadOnlyIntegerProperty pageCountProperty() {
        return pageCount.getReadOnlyProperty();
    }

    /** @return the decoded image for the current page, or {@code null} */
    public ReadOnlyObjectProperty<Image> currentImageProperty() {
        return currentImage.getReadOnlyProperty();
    }

    /** @return whether there is a page after the current one */
    public ReadOnlyBooleanProperty canGoNextProperty() {
        return canGoNext.getReadOnlyProperty();
    }

    /** @return whether there is a page before the current one */
    public ReadOnlyBooleanProperty canGoPreviousProperty() {
        return canGoPrevious.getReadOnlyProperty();
    }

    // ------------------------------------------------------------------
    // Writable presentation axes
    // ------------------------------------------------------------------

    /** @return the reading-mode axis (single page, webtoon, ...) */
    public ObjectProperty<ReadingMode> readingModeProperty() {
        return readingMode;
    }

    /** @return the reading-direction axis (LTR / RTL) */
    public ObjectProperty<ReadingDirection> readingDirectionProperty() {
        return readingDirection;
    }

    /** @return the fit-mode axis (fit screen, fit width, ...) */
    public ObjectProperty<FitMode> fitModeProperty() {
        return fitMode;
    }

    // ------------------------------------------------------------------
    // Commands
    // ------------------------------------------------------------------

    /**
     * Opens a chapter for reading, starting at page 1.
     *
     * @param chapter the chapter to read
     */
    public void open(Chapter chapter) {
        this.chapter.set(Objects.requireNonNull(chapter, "chapter"));
        currentPageIndex.set(1);
    }

    /** Loads the next page, if any. */
    public void goNext() {
        if (canGoNext.get()) {
            currentPageIndex.set(currentPageIndex.get() + 1);
        } else {
            messageHandler.accept("You have reached the end.");
        }
    }

    /** Loads the previous page, if any. */
    public void goPrevious() {
        if (canGoPrevious.get()) {
            currentPageIndex.set(currentPageIndex.get() - 1);
        } else {
            messageHandler.accept("You are at the first page.");
        }
    }

    /**
     * Jumps directly to a 1-based page index, clamped into range.
     *
     * @param index the requested 1-based page index
     */
    public void goToPage(int index) {
        Chapter current = chapter.get();
        if (current == null) {
            return;
        }
        int clamped = Math.max(1, Math.min(index, current.getPageCount()));
        currentPageIndex.set(clamped);
    }

    /**
     * Registers the callback fired when the user wants to leave the reader.
     *
     * @param handler invoked on the UI thread when "back" is requested
     */
    public void setBackHandler(Runnable handler) {
        this.backHandler = Objects.requireNonNull(handler, "handler");
    }

    /**
     * Registers the callback that receives transient status messages.
     *
     * @param handler receives short user-facing strings
     */
    public void setMessageHandler(Consumer<String> handler) {
        this.messageHandler = Objects.requireNonNull(handler, "handler");
    }

    /** Requests navigation back to the home screen. */
    public void goBack() {
        backHandler.run();
    }

    // ------------------------------------------------------------------
    // Internals
    // ------------------------------------------------------------------

    /** Recomputes pageCount/canGoNext/canGoPrevious and triggers a page load. */
    private void refreshDerivedState() {
        Chapter current = chapter.get();
        if (current == null) {
            pageCount.set(0);
            canGoNext.set(false);
            canGoPrevious.set(false);
            currentImage.set(null);
            return;
        }

        pageCount.set(current.getPageCount());
        int index = currentPageIndex.get();
        canGoPrevious.set(index > 1);
        canGoNext.set(index < current.getPageCount());

        loadPage(current, index);
    }

    /**
     * Requests the image for {@code index} via the loader.
     *
     * <p>A monotonically increasing token guards against a slow decode for a
     * previous page clobbering the image for the page the user has since
     * navigated to.</p>
     *
     * @param chapter the chapter
     * @param index   the 1-based page index to load
     */
    private void loadPage(Chapter chapter, int index) {
        Optional<MangaPage> page = chapter.findPage(index);
        if (page.isEmpty()) {
            currentImage.set(null);
            return;
        }

        final int token = ++loadToken;
        Path file = page.get().getImageFile();
        imageLoader.load(file, image -> {
            // Ignore stale results that arrive after the user moved on.
            if (token == loadToken) {
                currentImage.set(image.orElse(null));
            }
        });
    }
}
