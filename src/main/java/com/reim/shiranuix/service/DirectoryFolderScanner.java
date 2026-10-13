package com.reim.shiranuix.service;

import com.reim.shiranuix.model.Chapter;
import com.reim.shiranuix.model.MangaPage;
import com.reim.shiranuix.util.ImageFormats;
import com.reim.shiranuix.util.NaturalSort;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Comparator;
import java.util.List;
import java.util.Optional;
import java.util.stream.IntStream;
import java.util.stream.Stream;

/**
 * Default {@link FolderScanner} that reads a plain directory of images.
 *
 * <p>Pages are ordered using {@link NaturalSort} so that names like
 * {@code 1, 2, 10} come out in the order a human expects. Non-image files and
 * sub-directories are ignored.</p>
 */
public final class DirectoryFolderScanner implements FolderScanner {

    /** Comparator that orders files by their natural file name. */
    private static final Comparator<Path> BY_NATURAL_NAME =
            Comparator.comparing(path -> path.getFileName().toString(),
                    NaturalSort.naturalOrder());

    @Override
    public Optional<Chapter> scan(Path folder) {
        if (folder == null || !Files.isDirectory(folder) || !Files.isReadable(folder)) {
            return Optional.empty();
        }

        try (Stream<Path> entries = Files.list(folder)) {
            List<Path> images = entries
                    .filter(Files::isRegularFile)
                    .filter(ImageFormats::isSupported)
                    .sorted(BY_NATURAL_NAME)
                    .toList();

            if (images.isEmpty()) {
                return Optional.empty();
            }

            List<MangaPage> pages = buildPages(images);
            String title = folder.getFileName() == null
                    ? folder.toString()
                    : folder.getFileName().toString();
            return Optional.of(new Chapter(folder, title, pages));
        } catch (IOException e) {
            return Optional.empty();
        }
    }

    /** Converts ordered file paths into 1-based {@link MangaPage}s. */
    private static List<MangaPage> buildPages(List<Path> images) {
        return IntStream.range(0, images.size())
                .mapToObj(i -> new MangaPage(images.get(i), i + 1))
                .toList();
    }
}
