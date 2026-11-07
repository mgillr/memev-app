/* SPDX-License-Identifier: Apache-2.0 */
#include "ui.h"
#include "read.h"

#include <adwaita.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

typedef struct {
  const char *id;
  const char *label;
  int kind; /* 0 overview, 1 health, 2 later */
  const char *later;
} NavItem;

static const NavItem NAV[] = {
  {"overview", "Overview", 0, NULL},
  {"operations", "Operations", 2, "Not in this slice. Live agent panes are not drawn here."},
  {"projects", "Projects", 2, "Not in this slice. No project list is loaded yet."},
  {"bots", "Bots and agents", 2, "Not in this slice. The roster is not loaded yet."},
  {"memory", "Memory", 2, "Not in this slice. Search stays on the memev command until a later slice."},
  {"checked", "Checked work", 2, "Not in this slice. Receipts are not listed yet."},
  {"settings", "Settings", 2, "Not in this slice. The theme follows this computer."},
};
static const int NAV_COUNT = 7;

typedef struct {
  GtkApplication *application;
  GtkWidget *window;
  GtkWidget *sidebar;
  GtkWidget *health_btn;
  GtkWidget *nav_btns[8];
  GtkWidget *page_title;
  GtkWidget *overview_sentence;
  GtkWidget *north_star;
  GtkWidget *attention;
  GtkWidget *health_sentence;
  GtkWidget *checks;
  GtkWidget *later_body;
  GtkWidget *stack;
  GtkWidget *primary;
  MemevGlance glance;
  int selected;
  int attention_rows;
  int self_check;
} App;

static const char *CSS =
  "window.dark { background-color: #0A0D12; color: #E8ECF2; }"
  "window.dark .sidebar, window.dark .page { background-color: #0A0D12; }"
  "window.dark label { color: #E8ECF2; }"
  "window.dark label.dim, window.dark label.section { color: #7F8997; }"
  "window.dark label.sub { color: #A4ADBA; }"
  "window.dark button.nav label, window.dark button.health-link label { color: #A4ADBA; }"
  "window.dark button.nav.selected { background-color: #181E27; }"
  "window.dark button.nav.selected label { color: #E8ECF2; }"
  "window.dark .rule, window.dark .edge { background-color: #374252; }"
  "window.dark label.bad { color: #FF8A7A; }"
  "window.dark label.warn { color: #F2B544; }"
  "window.light { background-color: #F6F7F9; color: #10151C; }"
  "window.light .sidebar, window.light .page { background-color: #F6F7F9; }"
  "window.light label { color: #10151C; }"
  "window.light label.dim, window.light label.section { color: #5F6978; }"
  "window.light label.sub { color: #46505E; }"
  "window.light button.nav label, window.light button.health-link label { color: #46505E; }"
  "window.light button.nav.selected { background-color: #EDEFF3; }"
  "window.light button.nav.selected label { color: #10151C; }"
  "window.light .rule, window.light .edge { background-color: #C3CAD4; }"
  "window.light label.bad { color: #B3261E; }"
  "window.light label.warn { color: #8A5600; }"
  "label.dim, label.section { font-size: 12px; }"
  "label.section { letter-spacing: 0.06em; }"
  "label.sub { font-size: 16px; }"
  "label.title { font-size: 28px; font-weight: 500; }"
  "label.wordmark { font-size: 20px; font-weight: 500; }"
  ".amber-mark { background-color: #F2B544; }"
  "button.nav, button.health-link {"
  "  background-color: transparent; background-image: none; border-radius: 0;"
  "  box-shadow: none; border: none; min-height: 32px; padding: 4px 12px;"
  "}"
  "button.nav.selected { box-shadow: inset 2px 0 0 #F2B544; }"
  "button.primary {"
  "  background-color: #F2B544; background-image: none; color: #0A0D12;"
  "  min-height: 32px; min-width: 32px; padding: 4px 16px; border-radius: 6px;"
  "}"
  "button.primary label { color: #0A0D12; }"
  "row, list { background-color: transparent; }";

static void apply_theme(App *a) {
  const char *force = getenv("MEMEV_THEME");
  AdwStyleManager *style = adw_style_manager_get_default();
  gboolean dark = TRUE;
  if (force && strcmp(force, "light") == 0) {
    adw_style_manager_set_color_scheme(style, ADW_COLOR_SCHEME_FORCE_LIGHT);
    dark = FALSE;
  } else if (force && strcmp(force, "dark") == 0) {
    adw_style_manager_set_color_scheme(style, ADW_COLOR_SCHEME_FORCE_DARK);
    dark = TRUE;
  } else {
    adw_style_manager_set_color_scheme(style, ADW_COLOR_SCHEME_DEFAULT);
    dark = adw_style_manager_get_dark(style);
  }
  if (dark) {
    gtk_widget_add_css_class(a->window, "dark");
    gtk_widget_remove_css_class(a->window, "light");
  } else {
    gtk_widget_add_css_class(a->window, "light");
    gtk_widget_remove_css_class(a->window, "dark");
  }
}

static GtkWidget *section_head(const char *text) {
  GtkWidget *row = gtk_box_new(GTK_ORIENTATION_HORIZONTAL, 8);
  GtkWidget *mark = gtk_box_new(GTK_ORIENTATION_HORIZONTAL, 0);
  gtk_widget_add_css_class(mark, "amber-mark");
  gtk_widget_set_size_request(mark, 16, 2);
  gtk_widget_set_valign(mark, GTK_ALIGN_CENTER);
  GtkWidget *label = gtk_label_new(text);
  gtk_widget_add_css_class(label, "section");
  GtkWidget *rule = gtk_box_new(GTK_ORIENTATION_HORIZONTAL, 0);
  gtk_widget_add_css_class(rule, "rule");
  gtk_widget_set_hexpand(rule, TRUE);
  gtk_widget_set_size_request(rule, 1, 1);
  gtk_widget_set_valign(rule, GTK_ALIGN_CENTER);
  gtk_box_append(GTK_BOX(row), mark);
  gtk_box_append(GTK_BOX(row), label);
  gtk_box_append(GTK_BOX(row), rule);
  return row;
}

static void clear_list(GtkWidget *list) {
  GtkWidget *child;
  while ((child = gtk_widget_get_first_child(list)) != NULL)
    gtk_list_box_remove(GTK_LIST_BOX(list), child);
}

static GtkWidget *check_row(const char *title, const char *status) {
  char line[192];
  snprintf(line, sizeof line, "%s: %s", title, status);
  GtkWidget *label = gtk_label_new(line);
  gtk_label_set_xalign(GTK_LABEL(label), 0);
  gtk_label_set_wrap(GTK_LABEL(label), TRUE);
  if (strcmp(status, "fail") == 0) gtk_widget_add_css_class(label, "bad");
  else if (strcmp(status, "ok") != 0) gtk_widget_add_css_class(label, "warn");
  return label;
}

static void fill(App *a) {
  char health[256];
  char overview[512];
  memev_health_sentence(&a->glance, health, sizeof health);
  memev_overview_sentence(&a->glance, overview, sizeof overview);
  gtk_button_set_label(GTK_BUTTON(a->health_btn), health);
  gtk_label_set_text(GTK_LABEL(a->overview_sentence), overview);
  gtk_label_set_text(GTK_LABEL(a->health_sentence), health);
  if (a->glance.north_star[0]) {
    gtk_label_set_text(GTK_LABEL(a->north_star), a->glance.north_star);
    gtk_widget_set_visible(a->north_star, TRUE);
  } else {
    gtk_label_set_text(GTK_LABEL(a->north_star), "");
    gtk_widget_set_visible(a->north_star, FALSE);
  }

  clear_list(a->attention);
  a->attention_rows = 0;
  if (!a->glance.daemon_up) {
    gtk_list_box_append(GTK_LIST_BOX(a->attention), gtk_label_new("Nothing to list while Memev is not running."));
  } else if (a->glance.checks_fail + a->glance.checks_warn == 0) {
    gtk_list_box_append(GTK_LIST_BOX(a->attention), gtk_label_new("Nothing needs you."));
  } else {
    for (int i = 0; i < a->glance.check_shown; i++) {
      if (strcmp(a->glance.checks[i].status, "ok") == 0) continue;
      gtk_list_box_append(GTK_LIST_BOX(a->attention), check_row(a->glance.checks[i].title, a->glance.checks[i].status));
      a->attention_rows++;
    }
  }

  clear_list(a->checks);
  if (!a->glance.daemon_up || a->glance.check_shown == 0) {
    gtk_list_box_append(GTK_LIST_BOX(a->checks), gtk_label_new("No checks were returned."));
  } else {
    for (int i = 0; i < a->glance.check_shown; i++)
      gtk_list_box_append(GTK_LIST_BOX(a->checks), check_row(a->glance.checks[i].title, a->glance.checks[i].status));
  }
}

static void show_page(App *a, int index) {
  if (index < 0 || index >= NAV_COUNT) return;
  a->selected = index;
  for (int i = 0; i < NAV_COUNT; i++) {
    if (i == index) gtk_widget_add_css_class(a->nav_btns[i], "selected");
    else gtk_widget_remove_css_class(a->nav_btns[i], "selected");
  }
  const NavItem *item = &NAV[index];
  if (item->kind == 0) {
    gtk_label_set_text(GTK_LABEL(a->page_title), "Executive overview");
    gtk_stack_set_visible_child_name(GTK_STACK(a->stack), "overview");
  } else if (item->kind == 1) {
    gtk_label_set_text(GTK_LABEL(a->page_title), "Health");
    gtk_stack_set_visible_child_name(GTK_STACK(a->stack), "health");
  } else {
    gtk_label_set_text(GTK_LABEL(a->page_title), item->label);
    gtk_label_set_text(GTK_LABEL(a->later_body), item->later);
    gtk_stack_set_visible_child_name(GTK_STACK(a->stack), "later");
  }
}

static void align_button_label(GtkWidget *button) {
  GtkWidget *child = gtk_button_get_child(GTK_BUTTON(button));
  if (GTK_IS_LABEL(child)) {
    gtk_label_set_xalign(GTK_LABEL(child), 0);
    gtk_widget_set_halign(child, GTK_ALIGN_START);
    gtk_widget_set_hexpand(child, TRUE);
  }
}

static void on_nav(GtkButton *button, gpointer ud) {
  App *a = ud;
  int index = 0;
  for (int i = 0; i < NAV_COUNT; i++) {
    if (a->nav_btns[i] == GTK_WIDGET(button)) index = i;
  }
  show_page(a, index);
}

static void on_health_link(GtkButton *button, gpointer ud) {
  (void)button;
  App *a = ud;
  show_page(a, 0);
  /* Health is not a menu row. Open the health page directly. */
  gtk_label_set_text(GTK_LABEL(a->page_title), "Health");
  gtk_stack_set_visible_child_name(GTK_STACK(a->stack), "health");
  for (int i = 0; i < NAV_COUNT; i++) gtk_widget_remove_css_class(a->nav_btns[i], "selected");
  a->selected = -1;
}

static void reload_glance(App *a) {
  memev_load_glance(&a->glance);
  fill(a);
}

static void on_refresh(GSimpleAction *action, GVariant *param, gpointer ud) {
  (void)action;
  (void)param;
  reload_glance(ud);
}

static void on_refresh_clicked(GtkButton *button, gpointer ud) {
  (void)button;
  reload_glance(ud);
}

static gboolean poll_glance(gpointer ud) {
  reload_glance(ud);
  return G_SOURCE_CONTINUE;
}

static int count_class(GtkWidget *w, const char *class_name) {
  int n = 0;
  if (GTK_IS_BUTTON(w) && gtk_widget_has_css_class(w, class_name)) n++;
  for (GtkWidget *c = gtk_widget_get_first_child(w); c; c = gtk_widget_get_next_sibling(c))
    n += count_class(c, class_name);
  return n;
}

static int count_focusable_buttons(GtkWidget *w) {
  int n = 0;
  if (GTK_IS_BUTTON(w) && gtk_widget_get_can_focus(w) && gtk_widget_get_sensitive(w)) n++;
  for (GtkWidget *c = gtk_widget_get_first_child(w); c; c = gtk_widget_get_next_sibling(c))
    n += count_focusable_buttons(c);
  return n;
}

static int text_has(GtkWidget *w, const char *needle) {
  if (GTK_IS_LABEL(w)) {
    const char *t = gtk_label_get_text(GTK_LABEL(w));
    if (t && strstr(t, needle)) return 1;
  }
  if (GTK_IS_BUTTON(w)) {
    const char *t = gtk_button_get_label(GTK_BUTTON(w));
    if (t && strstr(t, needle)) return 1;
  }
  for (GtkWidget *c = gtk_widget_get_first_child(w); c; c = gtk_widget_get_next_sibling(c))
    if (text_has(c, needle)) return 1;
  return 0;
}

static int list_len(GtkWidget *list) {
  int n = 0;
  for (GtkWidget *c = gtk_widget_get_first_child(list); c; c = gtk_widget_get_next_sibling(c)) n++;
  return n;
}

static gboolean report_and_quit(gpointer ud) {
  App *a = ud;
  int w = 0, h = 0;
  gtk_widget_get_size_request(a->window, &w, &h);
  int sw = 0, sh = 0;
  gtk_widget_get_size_request(a->sidebar, &sw, &sh);
  int primary = count_class(a->window, "primary");
  int focusable = count_focusable_buttons(a->window);
  int secret = text_has(a->window, "should-not-appear") || text_has(a->window, "vault-marker");
  char **accels = gtk_application_get_accels_for_action(a->application, "win.refresh");
  int accel = accels && accels[0] ? 1 : 0;
  g_strfreev(accels);
  char health[256];
  char overview[512];
  memev_health_sentence(&a->glance, health, sizeof health);
  memev_overview_sentence(&a->glance, overview, sizeof overview);
  const char *shown_h = gtk_button_get_label(GTK_BUTTON(a->health_btn));
  const char *shown_o = gtk_label_get_text(GTK_LABEL(a->overview_sentence));
  int match = shown_h && shown_o && strcmp(shown_h, health) == 0 && strcmp(shown_o, overview) == 0;
  printf("health=%s\n", shown_h ? shown_h : "");
  printf("overview=%s\n", shown_o ? shown_o : "");
  printf("star=%s\n", a->glance.north_star);
  printf("attention=%d\n", a->attention_rows);
  printf("checks=%d\n", list_len(a->checks));
  printf("primary=%d\n", primary);
  printf("minw=%d\n", w);
  printf("minh=%d\n", h);
  printf("sidebar=%d\n", sw);
  printf("focusable=%d\n", focusable);
  printf("accel=%d\n", accel);
  printf("match=%d\n", match);
  printf("secret=%s\n", secret ? "present" : "absent");
  int rc = 0;
  if (!match || primary != 1 || w < 1024 || h < 700 || sw != 240 || focusable < 9 || !accel || secret)
    rc = 1;
  exit(rc);
  return G_SOURCE_REMOVE;
}

static void on_help(GtkButton *button, gpointer ud) {
  (void)button;
  App *a = ud;
  for (int i = 0; i < NAV_COUNT; i++) gtk_widget_remove_css_class(a->nav_btns[i], "selected");
  a->selected = -1;
  gtk_label_set_text(GTK_LABEL(a->page_title), "Help and support");
  gtk_label_set_text(GTK_LABEL(a->later_body), "Help and support is not in this slice. This app sends nothing.");
  gtk_stack_set_visible_child_name(GTK_STACK(a->stack), "later");
}

static void on_activate(GtkApplication *application, gpointer ud) {
  App *a = ud;
  a->application = application;
  a->window = adw_application_window_new(application);
  gtk_window_set_title(GTK_WINDOW(a->window), "Memev");
  gtk_window_set_default_size(GTK_WINDOW(a->window), 1280, 800);
  gtk_widget_set_size_request(a->window, 1024, 700);

  GtkCssProvider *css = gtk_css_provider_new();
  gtk_css_provider_load_from_string(css, CSS);
  gtk_style_context_add_provider_for_display(
    gdk_display_get_default(),
    GTK_STYLE_PROVIDER(css),
    GTK_STYLE_PROVIDER_PRIORITY_APPLICATION);
  g_object_unref(css);
  apply_theme(a);

  GtkWidget *root = gtk_box_new(GTK_ORIENTATION_HORIZONTAL, 0);
  a->sidebar = gtk_box_new(GTK_ORIENTATION_VERTICAL, 8);
  gtk_widget_add_css_class(a->sidebar, "sidebar");
  gtk_widget_set_size_request(a->sidebar, 240, -1);
  gtk_widget_set_margin_top(a->sidebar, 16);
  gtk_widget_set_margin_bottom(a->sidebar, 16);
  gtk_widget_set_margin_start(a->sidebar, 12);
  gtk_widget_set_margin_end(a->sidebar, 12);

  GtkWidget *word = gtk_label_new("Memev");
  gtk_widget_add_css_class(word, "wordmark");
  gtk_label_set_xalign(GTK_LABEL(word), 0);
  gtk_box_append(GTK_BOX(a->sidebar), word);

  a->health_btn = gtk_button_new_with_label("Reading Memev.");
  gtk_widget_add_css_class(a->health_btn, "health-link");
  gtk_label_set_wrap(GTK_LABEL(gtk_button_get_child(GTK_BUTTON(a->health_btn))), TRUE);
  align_button_label(a->health_btn);
  g_signal_connect(a->health_btn, "clicked", G_CALLBACK(on_health_link), a);
  gtk_box_append(GTK_BOX(a->sidebar), a->health_btn);

  for (int i = 0; i < NAV_COUNT; i++) {
    a->nav_btns[i] = gtk_button_new_with_label(NAV[i].label);
    gtk_widget_add_css_class(a->nav_btns[i], "nav");
    gtk_widget_set_halign(a->nav_btns[i], GTK_ALIGN_FILL);
    align_button_label(a->nav_btns[i]);
    g_signal_connect(a->nav_btns[i], "clicked", G_CALLBACK(on_nav), a);
    gtk_box_append(GTK_BOX(a->sidebar), a->nav_btns[i]);
  }
  GtkWidget *spacer = gtk_box_new(GTK_ORIENTATION_VERTICAL, 0);
  gtk_widget_set_vexpand(spacer, TRUE);
  gtk_box_append(GTK_BOX(a->sidebar), spacer);
  GtkWidget *help = gtk_button_new_with_label("Help and support");
  gtk_widget_add_css_class(help, "nav");
  align_button_label(help);
  g_signal_connect(help, "clicked", G_CALLBACK(on_help), a);
  gtk_box_append(GTK_BOX(a->sidebar), help);

  GtkWidget *edge = gtk_box_new(GTK_ORIENTATION_VERTICAL, 0);
  gtk_widget_add_css_class(edge, "edge");
  gtk_widget_set_size_request(edge, 1, -1);
  gtk_widget_set_vexpand(edge, TRUE);

  GtkWidget *page = gtk_box_new(GTK_ORIENTATION_VERTICAL, 24);
  gtk_widget_add_css_class(page, "page");
  gtk_widget_set_hexpand(page, TRUE);
  gtk_widget_set_margin_top(page, 24);
  gtk_widget_set_margin_bottom(page, 24);
  gtk_widget_set_margin_start(page, 36);
  gtk_widget_set_margin_end(page, 36);

  GtkWidget *header = gtk_box_new(GTK_ORIENTATION_HORIZONTAL, 16);
  a->page_title = gtk_label_new("Executive overview");
  gtk_widget_add_css_class(a->page_title, "title");
  gtk_label_set_xalign(GTK_LABEL(a->page_title), 0);
  gtk_widget_set_hexpand(a->page_title, TRUE);
  a->primary = gtk_button_new_with_label("Refresh");
  gtk_widget_add_css_class(a->primary, "primary");
  g_signal_connect(a->primary, "clicked", G_CALLBACK(on_refresh_clicked), a);
  gtk_box_append(GTK_BOX(header), a->page_title);
  gtk_box_append(GTK_BOX(header), a->primary);
  gtk_box_append(GTK_BOX(page), header);

  a->stack = gtk_stack_new();
  gtk_widget_set_vexpand(a->stack, TRUE);

  GtkWidget *overview = gtk_box_new(GTK_ORIENTATION_VERTICAL, 24);
  a->overview_sentence = gtk_label_new("");
  gtk_widget_add_css_class(a->overview_sentence, "sub");
  gtk_label_set_xalign(GTK_LABEL(a->overview_sentence), 0);
  gtk_label_set_wrap(GTK_LABEL(a->overview_sentence), TRUE);
  gtk_label_set_max_width_chars(GTK_LABEL(a->overview_sentence), 64);
  a->north_star = gtk_label_new("");
  gtk_label_set_xalign(GTK_LABEL(a->north_star), 0);
  gtk_label_set_wrap(GTK_LABEL(a->north_star), TRUE);
  gtk_box_append(GTK_BOX(overview), a->overview_sentence);
  gtk_box_append(GTK_BOX(overview), a->north_star);
  gtk_box_append(GTK_BOX(overview), section_head("STATUS"));
  a->attention = gtk_list_box_new();
  gtk_list_box_set_selection_mode(GTK_LIST_BOX(a->attention), GTK_SELECTION_NONE);
  gtk_box_append(GTK_BOX(overview), a->attention);
  GtkWidget *note = gtk_label_new("Charts are a later slice. The lines above are the core's answers, read just now. No sample figures are shown.");
  gtk_widget_add_css_class(note, "dim");
  gtk_label_set_xalign(GTK_LABEL(note), 0);
  gtk_label_set_wrap(GTK_LABEL(note), TRUE);
  gtk_label_set_max_width_chars(GTK_LABEL(note), 64);
  gtk_box_append(GTK_BOX(overview), note);

  GtkWidget *health = gtk_box_new(GTK_ORIENTATION_VERTICAL, 24);
  a->health_sentence = gtk_label_new("");
  gtk_widget_add_css_class(a->health_sentence, "sub");
  gtk_label_set_xalign(GTK_LABEL(a->health_sentence), 0);
  gtk_label_set_wrap(GTK_LABEL(a->health_sentence), TRUE);
  a->checks = gtk_list_box_new();
  gtk_list_box_set_selection_mode(GTK_LIST_BOX(a->checks), GTK_SELECTION_NONE);
  gtk_box_append(GTK_BOX(health), a->health_sentence);
  gtk_box_append(GTK_BOX(health), section_head("CHECKS"));
  gtk_box_append(GTK_BOX(health), a->checks);

  GtkWidget *later = gtk_box_new(GTK_ORIENTATION_VERTICAL, 16);
  a->later_body = gtk_label_new("");
  gtk_widget_add_css_class(a->later_body, "sub");
  gtk_label_set_xalign(GTK_LABEL(a->later_body), 0);
  gtk_label_set_wrap(GTK_LABEL(a->later_body), TRUE);
  gtk_label_set_max_width_chars(GTK_LABEL(a->later_body), 64);
  gtk_box_append(GTK_BOX(later), a->later_body);

  gtk_stack_add_named(GTK_STACK(a->stack), overview, "overview");
  gtk_stack_add_named(GTK_STACK(a->stack), health, "health");
  gtk_stack_add_named(GTK_STACK(a->stack), later, "later");
  gtk_box_append(GTK_BOX(page), a->stack);

  GtkWidget *scroll = gtk_scrolled_window_new();
  gtk_scrolled_window_set_policy(GTK_SCROLLED_WINDOW(scroll), GTK_POLICY_NEVER, GTK_POLICY_AUTOMATIC);
  gtk_scrolled_window_set_child(GTK_SCROLLED_WINDOW(scroll), page);
  gtk_widget_set_hexpand(scroll, TRUE);

  gtk_box_append(GTK_BOX(root), a->sidebar);
  gtk_box_append(GTK_BOX(root), edge);
  gtk_box_append(GTK_BOX(root), scroll);
  adw_application_window_set_content(ADW_APPLICATION_WINDOW(a->window), root);

  GSimpleAction *refresh = g_simple_action_new("refresh", NULL);
  g_signal_connect(refresh, "activate", G_CALLBACK(on_refresh), a);
  g_action_map_add_action(G_ACTION_MAP(a->window), G_ACTION(refresh));
  const char *accels[] = {"<Control>r", NULL};
  gtk_application_set_accels_for_action(application, "win.refresh", accels);

  reload_glance(a);
  show_page(a, 0);
  gtk_window_present(GTK_WINDOW(a->window));

  if (a->self_check) {
    g_idle_add(report_and_quit, a);
    return;
  }
  const char *poll = getenv("MEMEV_POLL_MS");
  int ms = poll ? atoi(poll) : 5000;
  if (ms < 200) ms = 200;
  g_timeout_add(ms, poll_glance, a);
}

int app_run(int argc, char **argv) {
  if (argc > 1 && strcmp(argv[1], "--version") == 0) {
    printf("memev-desktop 0.0.1-slice1\n");
    return 0;
  }
  App app;
  memset(&app, 0, sizeof app);
  app.self_check = getenv("MEMEV_SELF_CHECK") != NULL;
  AdwApplication *application = adw_application_new("computer.memev.Desktop", G_APPLICATION_DEFAULT_FLAGS);
  g_signal_connect(application, "activate", G_CALLBACK(on_activate), &app);
  int rc = g_application_run(G_APPLICATION(application), argc, argv);
  g_object_unref(application);
  return rc;
}
