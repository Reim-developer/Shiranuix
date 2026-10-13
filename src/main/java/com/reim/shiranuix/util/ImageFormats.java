package com.reim.shiranuix.util;

import java.nio.file.Path;
import java.util.Locale;
import java.util.Set;

/**
 * Recognises image file extensions that the reader can display.
 *
 * <p>Kept in one place so the folder scanner, the home screen, and the
 * (future) library indexer all agree on which files count as pages.</p>
 */
public final class ImageFormats {

    /**
     * Supported extensions, lowercase and without the leading dot.
     *
     * <p>{@code webp} is listed for forward compatibility; JavaFX does not
     * decode it natively, so {@link #isSupported(Path)} should be consulted
     * rather than trusting this set blindly in new code.</p>
     */
    public static final Set<String> SUPPORTED_EXTENSIONS = Set.of(
            "jpg", "jpeg", "png", "gif", "bmp", "webp");

    /** Private utility constructor. */
    private ImageFormats() {
    }

    /**
     * Checks whether the given file looks like a supported image.
     *
     * <p>Only the extension is inspected; the file is not opened or decoded.
     * A {@code .jpg} that is actually a text file would still return
     * {@code true}.</p>
     *
     * @param file the file to test
     * @return {@code true} if the extension is a supported image format
     */
    public static boolean isSupported(Path file) {
        if (file == null) {
            return false;
        }
        String name = file.getFileName() == null
                ? file.toString()
                : file.getFileName().toString();
        int dot = name.lastIndexOf('.');
        if (dot < 0 || dot == name.length() - 1) {
            return false;
        }
        String ext = name.substring(dot + 1).toLowerCase(Locale.ROOT);
        return SUPPORTED_EXTENSIONS.contains(ext);
    }
}
