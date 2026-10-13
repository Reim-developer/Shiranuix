package com.reim.shiranuix.ui.theme;

import com.reim.shiranuix.model.ThemeMode;
import java.util.Objects;
import javafx.beans.property.ObjectProperty;
import javafx.beans.property.SimpleObjectProperty;
import javafx.scene.Parent;
import javafx.scene.Scene;
import javafx.stage.Window;

/**
 * Applies and switches the application colour theme at runtime.
 *
 * <p>Themes are plain JavaFX stylesheets loaded from the classpath. Switching
 * a theme swaps the stylesheet list on the scene and nudges any attached
 * windows so the change takes effect immediately.</p>
 *
 * <p>The manager keeps the requested {@link ThemeMode} separate from the
 * currently applied concrete stylesheet, so {@code SYSTEM} can be resolved
 * against the platform preference without losing the user's intent.</p>
 */
public final class ThemeManager {

    private static final String LIGHT_CSS = "/css/theme-light.css";
    private static final String DARK_CSS  = "/css/theme-dark.css";

    private final ObjectProperty<ThemeMode> mode =
            new SimpleObjectProperty<>(ThemeMode.SYSTEM);

    private Scene boundScene;

    /** Creates a theme manager defaulting to {@link ThemeMode#SYSTEM}. */
    public ThemeManager() {
        // Re-apply the stylesheet whenever the requested mode changes.
        mode.addListener((obs, old, value) -> applyIfPossible());
    }

    /**
     * @return the requested theme mode (may be {@code SYSTEM})
     */
    public ObjectProperty<ThemeMode> modeProperty() {
        return mode;
    }

    /**
     * Binds this manager to a scene so future mode changes apply live.
     *
     * @param scene the scene whose stylesheets will be managed
     */
    public void bind(Scene scene) {
        this.boundScene = Objects.requireNonNull(scene, "scene");
        applyIfPossible();
    }

    /**
     * Cycles to the next theme mode: system → light → dark → system.
     *
     * <p>Used by the toolbar theme-toggle button.</p>
     */
    public void cycle() {
        switch (mode.get()) {
            case SYSTEM -> mode.set(ThemeMode.LIGHT);
            case LIGHT  -> mode.set(ThemeMode.DARK);
            case DARK   -> mode.set(ThemeMode.SYSTEM);
        }
    }

    /**
     * Resolves the current mode to a concrete CSS resource path.
     *
     * <p>{@code SYSTEM} is resolved against the platform preference when it
     * is queryable, defaulting to light otherwise.</p>
     *
     * @return the classpath path of the stylesheet to apply
     */
    public String resolveCss() {
        boolean dark = switch (mode.get()) {
            case DARK   -> true;
            case LIGHT  -> false;
            case SYSTEM -> isPlatformDark();
        };
        return dark ? DARK_CSS : LIGHT_CSS;
    }

    /** Applies the resolved stylesheet to the bound scene, if any. */
    private void applyIfPossible() {
        if (boundScene == null) {
            return;
        }

        String css = resolveCss();
        var stylesheets = boundScene.getStylesheets();
        stylesheets.removeAll(LIGHT_CSS, DARK_CSS);
        stylesheets.add(css);

        // Force a repaint on attached windows so the swap is instant.
        for (Window window : boundScene.getWindow() == null
                ? java.util.List.<Window>of()
                : java.util.List.of(boundScene.getWindow())) {
            window.sizeToScene();
        }
    }

    /**
     * Best-effort detection of the OS dark preference.
     *
     * <p>JavaFX has no portable API for this. On Windows we read the registry
     * via a system property the platform may expose; elsewhere we fall back to
     * {@code false} (light). This is intentionally lenient — a wrong guess only
     * affects the initial theme, which the user can toggle.</p>
     *
     * @return {@code true} if the platform reports a dark preference
     */
    private static boolean isPlatformDark() {
        // JavaFX exposes the user's desktop theme hint on some platforms.
        String hint = System.getProperty("javafx.theme", "");
        return "dark".equalsIgnoreCase(hint);
    }
}
