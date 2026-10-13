package com.reim.shiranuix.ui.home;

import com.reim.shiranuix.model.LibraryEntry;
import java.nio.file.Path;
import java.util.List;
import java.util.Objects;
import javafx.fxml.FXML;
import javafx.geometry.Pos;
import javafx.scene.control.Button;
import javafx.scene.control.Label;
import javafx.scene.control.ScrollPane;
import javafx.scene.control.Tooltip;
import javafx.scene.layout.HBox;
import javafx.scene.layout.Priority;
import javafx.scene.layout.Region;
import javafx.scene.layout.StackPane;
import javafx.scene.layout.TilePane;
import javafx.scene.layout.VBox;
import javafx.stage.DirectoryChooser;

/**
 * Code-behind for {@code home-view.fxml}.
 *
 * <p>Follows the MVVM rule: this controller only wires the view to the
 * {@link HomeViewModel} and handles pure-view concerns (opening a native
 * directory chooser, building card nodes). All state and decisions live in
 * the view model, which is unit-tested separately.</p>
 *
 * <p>The view model is injected via {@link #setViewModel(HomeViewModel)}
 * after the FXML is loaded, rather than through an {@code initialize}
 * overload, because FXMLLoader only invokes a no-arg {@code initialize()}.</p>
 */
public final class HomeController {

    @FXML private StackPane contentStack;
    @FXML private Label placeholderLabel;
    @FXML private ScrollPane scrollPane;
    @FXML private TilePane folderTilePane;
    @FXML private Label subtitleLabel;
    @FXML private Label statusLabel;
    @FXML private Button openFolderButton;

    private HomeViewModel viewModel;

    /**
     * Injects the view model and binds it to the view.
     *
     * <p>Called once, immediately after the FXML is loaded and before the
     * view is shown.</p>
     *
     * @param viewModel the view model backing this screen
     */
    public void setViewModel(HomeViewModel viewModel) {
        this.viewModel = Objects.requireNonNull(viewModel, "viewModel");

        openFolderButton.setOnAction(event -> chooseAndOpenFolder());

        // Rebuild the card grid whenever the recent list changes.
        viewModel.recentProperty().addListener((obs, old, entries) -> renderCards(entries));

        // Mirror the status line and toggle the empty-state placeholder.
        statusLabel.textProperty().bind(viewModel.statusProperty());
        placeholderLabel.visibleProperty().bind(
                viewModel.recentProperty().emptyProperty());
        placeholderLabel.managedProperty().bind(
                viewModel.recentProperty().emptyProperty());
        scrollPane.visibleProperty().bind(
                viewModel.recentProperty().emptyProperty().not());
        scrollPane.managedProperty().bind(
                viewModel.recentProperty().emptyProperty().not());

        renderCards(viewModel.getRecent());
    }

    /**
     * Opens a native directory picker and hands the choice to the view model.
     */
    private void chooseAndOpenFolder() {
        DirectoryChooser chooser = new DirectoryChooser();
        chooser.setTitle("Open a folder of images");

        // Start from the last-used folder if one was recorded.
        List<LibraryEntry> recent = viewModel.getRecent();
        if (!recent.isEmpty()) {
            Path last = recent.get(0).getFolder().getParent();
            if (last != null && last.toFile().isDirectory()) {
                chooser.setInitialDirectory(last.toFile());
            }
        }

        var selected = chooser.showDialog(openFolderButton.getScene().getWindow());
        if (selected != null) {
            viewModel.openFolder(selected.toPath());
        }
    }

    /**
     * Rebuilds the card grid from the given entries.
     *
     * @param entries the recent folders to display, most recent first
     */
    private void renderCards(List<LibraryEntry> entries) {
        folderTilePane.getChildren().clear();
        for (LibraryEntry entry : entries) {
            folderTilePane.getChildren().add(buildCard(entry));
        }
    }

    /**
     * Builds a single folder card.
     *
     * @param entry the folder to represent
     * @return the card node
     */
    private VBox buildCard(LibraryEntry entry) {
        Label title = new Label(entry.getTitle());
        title.getStyleClass().add("folder-title");
        title.setWrapText(true);

        Label meta = new Label(entry.getPageCount() + " pages");
        meta.getStyleClass().add("folder-meta");

        VBox text = new VBox(4, title, meta);
        text.setAlignment(Pos.CENTER_LEFT);

        VBox card = new VBox(text);
        card.getStyleClass().add("folder-card");
        card.setPrefWidth(180);
        card.setMinWidth(Region.USE_PREF_SIZE);
        card.setMaxWidth(Region.USE_PREF_SIZE);
        VBox.setVgrow(text, Priority.ALWAYS);

        Tooltip.install(card, new Tooltip(entry.getFolder().toString()));
        card.setOnMouseClicked(event -> viewModel.openFolder(entry.getFolder()));

        return card;
    }
}
