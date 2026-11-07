/* SPDX-License-Identifier: Apache-2.0 */
#include "ui.h"
#include "read.h"
#include "guide.h"

#include <adwaita.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

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
  GtkWidget *sidebar_column;
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
  GtkWidget *back;
  GtkWidget *guide_text;
  GtkWidget *guide_entry;
  GtkWidget *guide_choices;
  GtkWidget *choice_this;
  GtkWidget *choice_other;
  GtkWidget *guide_apps;
  GtkWidget *guide_backup;
  GtkWidget *guide_detail;
  int in_guide;
  int guide_step;
  int app_count;
  GuidePlan plan;
  GuideApp apps[GUIDE_APPS_MAX];
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
  if (a->in_guide) {
    const char *title = gtk_label_get_text(GTK_LABEL(a->page_title));
    const char *button = gtk_button_get_label(GTK_BUTTON(a->primary));
    printf("guide=%s\n", title ? title : "");
    printf("primary_label=%s\n", button ? button : "");
    printf("primary=%d\n", count_class(a->window, "primary"));
    exit(title && button && strcmp(title, "Is this your first computer?") == 0 && strcmp(button, "Continue") == 0 && count_class(a->window, "primary") == 1 ? 0 : 1);
  }
  int w = 0, h = 0;
  gtk_widget_get_size_request(a->window, &w, &h);
  int sw = 0, sh = 0;
  gtk_widget_get_size_request(a->sidebar_column, &sw, &sh);
  int allocated = gtk_widget_get_width(a->sidebar_column);
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
  printf("sidebar=%d\n", allocated > 0 ? allocated : sw);
  printf("focusable=%d\n", focusable);
  printf("accel=%d\n", accel);
  printf("match=%d\n", match);
  printf("secret=%s\n", secret ? "present" : "absent");
  int rc = 0;
  if (!match || primary != 1 || w < 1024 || h < 700 || sw != 240 || allocated != 240 || focusable < 9 || !accel || secret)
    rc = 1;
  exit(rc);
  return G_SOURCE_REMOVE;
}

static int want_guide(void) {
  const char *flag = getenv("MEMEV_GUIDE");
  if (flag && strcmp(flag, "off") == 0) return 0;
  if (flag && strcmp(flag, "1") == 0) return 1;
  const char *cfg = getenv("MEMEV_CONFIG_DIR");
  char path[512];
  if (cfg && cfg[0]) snprintf(path, sizeof path, "%s/desktop-first-launch-done", cfg);
  else {
    const char *home = getenv("HOME");
    if (!home || !home[0]) return 1;
    snprintf(path, sizeof path, "%s/.config/memev/desktop-first-launch-done", home);
  }
  return access(path, F_OK) != 0;
}

static void write_guide_marker(void) {
  const char *cfg = getenv("MEMEV_CONFIG_DIR");
  char path[512];
  if (cfg && cfg[0]) snprintf(path, sizeof path, "%s/desktop-first-launch-done", cfg);
  else {
    const char *home = getenv("HOME");
    if (!home || !home[0]) return;
    snprintf(path, sizeof path, "%s/.config/memev/desktop-first-launch-done", home);
  }
  char dir[512];
  snprintf(dir, sizeof dir, "%s", path);
  char *slash = strrchr(dir, '/');
  if (slash) *slash = 0;
  g_mkdir_with_parents(dir, 0700);
  g_file_set_contents(path, "ok\n", -1, NULL);
}

static void guide_pick(GtkButton *button, gpointer ud) {
  App *a = ud;
  a->plan.first_computer = button == GTK_BUTTON(a->choice_this) ? 1 : 0;
  if (a->plan.first_computer == 1) {
    gtk_widget_add_css_class(a->choice_this, "selected");
    gtk_widget_remove_css_class(a->choice_other, "selected");
  } else {
    gtk_widget_add_css_class(a->choice_other, "selected");
    gtk_widget_remove_css_class(a->choice_this, "selected");
  }
}

static void guide_load_apps(App *a) {
  if (a->app_count > 0) return;
  char bin[4096];
  if (memev_resolve_bin(bin, sizeof bin) != 0) return;
  char out[8192];
  char *argv[] = {bin, "clients", NULL};
  int code = 0;
  if (memev_capture(bin, argv, out, sizeof out, 10000, &code) != 0 || code != 0) return;
  a->app_count = guide_parse_clients(out, a->apps, GUIDE_APPS_MAX);
  GtkWidget *child;
  while ((child = gtk_widget_get_first_child(a->guide_apps)) != NULL)
    gtk_box_remove(GTK_BOX(a->guide_apps), child);
  if (a->app_count == 0) {
    gtk_box_append(GTK_BOX(a->guide_apps), gtk_label_new("The client list did not parse."));
    return;
  }
  for (int i = 0; i < a->app_count; i++) {
    char line[180];
    snprintf(line, sizeof line, "%s: %s", a->apps[i].name, a->apps[i].state);
    GtkWidget *check = gtk_check_button_new_with_label(line);
    gtk_check_button_set_active(GTK_CHECK_BUTTON(check), FALSE);
    gtk_widget_set_sensitive(check, a->apps[i].selectable);
    g_object_set_data(G_OBJECT(check), "app-index", GINT_TO_POINTER(i));
    gtk_box_append(GTK_BOX(a->guide_apps), check);
  }
}

static void guide_show(App *a, int step) {
  a->in_guide = 1;
  a->guide_step = step;
  gtk_widget_set_visible(a->back, step > 0);
  gtk_widget_set_visible(a->guide_entry, FALSE);
  gtk_widget_set_visible(a->guide_choices, FALSE);
  gtk_widget_set_visible(a->guide_apps, FALSE);
  gtk_widget_set_visible(a->guide_backup, FALSE);
  gtk_label_set_text(GTK_LABEL(a->guide_detail), "");
  gtk_stack_set_visible_child_name(GTK_STACK(a->stack), "guide");
  gtk_button_set_label(GTK_BUTTON(a->primary), "Continue");
  if (step == 0) {
    gtk_label_set_text(GTK_LABEL(a->page_title), "Is this your first computer?");
    gtk_label_set_text(GTK_LABEL(a->guide_text), "Nothing is created, connected or sent on this step.");
    gtk_widget_set_visible(a->guide_choices, TRUE);
  } else if (step == 1 && a->plan.first_computer == 0) {
    gtk_label_set_text(GTK_LABEL(a->page_title), "Join another computer");
    gtk_label_set_text(GTK_LABEL(a->guide_text), "The join runs only when you press the button. It contacts the other computer.");
    gtk_widget_set_visible(a->guide_entry, TRUE);
    gtk_button_set_label(GTK_BUTTON(a->primary), "Join the other computer");
  } else if (step == 1) {
    gtk_label_set_text(GTK_LABEL(a->page_title), "Where should the notes live?");
    gtk_label_set_text(GTK_LABEL(a->guide_text), "Type a full path. The folder is created only when you continue, and only on this computer.");
    gtk_widget_set_visible(a->guide_entry, TRUE);
  } else if (step == 2) {
    gtk_label_set_text(GTK_LABEL(a->page_title), "Which apps should Memev know?");
    gtk_label_set_text(GTK_LABEL(a->guide_text), "The list is the core's own client list. A tick is remembered here. Nothing is connected: that list has no id, and this window does not keep its own list.");
    guide_load_apps(a);
    gtk_widget_set_visible(a->guide_apps, TRUE);
  } else if (step == 3) {
    gtk_label_set_text(GTK_LABEL(a->page_title), "Back up to your own git remote?");
    gtk_label_set_text(GTK_LABEL(a->guide_text), "Leave this blank to skip it. Recording a remote does not upload the notes, and nothing is recorded unless you tick the line.");
    gtk_widget_set_visible(a->guide_entry, TRUE);
    gtk_widget_set_visible(a->guide_backup, TRUE);
    gtk_check_button_set_active(GTK_CHECK_BUTTON(a->guide_backup), FALSE);
  } else {
    gtk_label_set_text(GTK_LABEL(a->page_title), "Check that a note can be saved");
    gtk_label_set_text(GTK_LABEL(a->guide_text), "This uses the core on this computer. It does not use sample figures.");
    gtk_button_set_label(GTK_BUTTON(a->primary), "Check the notes");
  }
}

static void guide_finish(App *a) {
  a->in_guide = 0;
  gtk_widget_set_visible(a->back, FALSE);
  gtk_button_set_label(GTK_BUTTON(a->primary), "Refresh");
  write_guide_marker();
  reload_glance(a);
  show_page(a, 0);
}

static void guide_collect(App *a) {
  const char *text = gtk_editable_get_text(GTK_EDITABLE(a->guide_entry));
  if (a->guide_step == 1 && a->plan.first_computer == 0) {
    snprintf(a->plan.join_code, sizeof a->plan.join_code, "%s", text ? text : "");
    a->plan.confirm_join = true;
  } else if (a->guide_step == 1) {
    snprintf(a->plan.notes_dir, sizeof a->plan.notes_dir, "%s", text ? text : "");
  } else if (a->guide_step == 2) {
    a->plan.selected_count = 0;
    for (GtkWidget *c = gtk_widget_get_first_child(a->guide_apps); c; c = gtk_widget_get_next_sibling(c)) {
      if (!GTK_IS_CHECK_BUTTON(c) || !gtk_check_button_get_active(GTK_CHECK_BUTTON(c))) continue;
      int index = GPOINTER_TO_INT(g_object_get_data(G_OBJECT(c), "app-index"));
      if (index < 0 || index >= a->app_count || a->plan.selected_count >= GUIDE_APPS_MAX) continue;
      snprintf(a->plan.selected[a->plan.selected_count], sizeof a->plan.selected[0], "%s", a->apps[index].name);
      a->plan.selected_count++;
    }
    a->plan.confirm_apps = a->plan.selected_count > 0;
  } else if (a->guide_step == 3) {
    snprintf(a->plan.backup_url, sizeof a->plan.backup_url, "%s", text ? text : "");
    a->plan.confirm_backup = gtk_check_button_get_active(GTK_CHECK_BUTTON(a->guide_backup));
  }
}

static void guide_next(App *a) {
  if (a->guide_step == 0) {
    if (a->plan.first_computer < 0) {
      gtk_label_set_text(GTK_LABEL(a->guide_detail), "Choose this computer or another one.");
      return;
    }
    gtk_editable_set_text(GTK_EDITABLE(a->guide_entry), "");
    guide_show(a, 1);
    return;
  }
  guide_collect(a);
  if (a->guide_step == 1 && a->plan.first_computer == 0) {
  char bin[4096];
  char detail[400];
  if (memev_resolve_bin(bin, sizeof bin) != 0 || guide_apply(&a->plan, bin, detail, sizeof detail) != 0) {
    gtk_label_set_text(GTK_LABEL(a->guide_detail), detail[0] ? detail : "Join did not run.");
      a->plan.confirm_join = false;
      return;
    }
    guide_finish(a);
    return;
  }
  if (a->guide_step == 1) {
    char why[256];
    GuideCmd cmds[GUIDE_CMD_MAX];
    if (guide_build(&a->plan, cmds, GUIDE_CMD_MAX, why, sizeof why) < 0) {
      gtk_label_set_text(GTK_LABEL(a->guide_detail), why[0] ? why : "That folder cannot be used.");
      return;
    }
    guide_show(a, 2);
    return;
  }
  if (a->guide_step == 2) {
    char why[256];
    GuideCmd cmds[GUIDE_CMD_MAX];
    guide_build(&a->plan, cmds, GUIDE_CMD_MAX, why, sizeof why);
    if (why[0]) gtk_label_set_text(GTK_LABEL(a->guide_detail), why);
    gtk_editable_set_text(GTK_EDITABLE(a->guide_entry), "");
    guide_show(a, 3);
    return;
  }
  if (a->guide_step == 3) {
    char why[256];
    GuideCmd cmds[GUIDE_CMD_MAX];
    if (guide_build(&a->plan, cmds, GUIDE_CMD_MAX, why, sizeof why) < 0) {
      gtk_label_set_text(GTK_LABEL(a->guide_detail), why[0] ? why : "That remote cannot be recorded.");
      return;
    }
    guide_show(a, 4);
    return;
  }
  char bin[4096];
  char detail[400];
  if (memev_resolve_bin(bin, sizeof bin) != 0 || guide_apply(&a->plan, bin, detail, sizeof detail) != 0) {
    gtk_label_set_text(GTK_LABEL(a->guide_detail), detail[0] ? detail : "The check did not pass.");
    return;
  }
  if (detail[0]) gtk_label_set_text(GTK_LABEL(a->guide_detail), detail);
  guide_finish(a);
}

static void on_back(GtkButton *button, gpointer ud) {
  (void)button;
  App *a = ud;
  if (!a->in_guide || a->guide_step <= 0) return;
  if (a->guide_step == 1) a->plan.confirm_join = false;
  guide_show(a, a->guide_step - 1);
}

static void on_primary(GtkButton *button, gpointer ud) {
  (void)button;
  App *a = ud;
  if (a->in_guide) guide_next(a);
  else reload_glance(a);
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
  /* The designed sidebar is 240 wide in total. Margins sit inside that, not outside it. */
  a->sidebar_column = gtk_box_new(GTK_ORIENTATION_VERTICAL, 0);
  gtk_widget_set_size_request(a->sidebar_column, 240, -1);
  gtk_widget_set_hexpand(a->sidebar_column, FALSE);
  a->sidebar = gtk_box_new(GTK_ORIENTATION_VERTICAL, 8);
  gtk_widget_add_css_class(a->sidebar, "sidebar");
  gtk_widget_set_hexpand(a->sidebar, TRUE);
  gtk_widget_set_vexpand(a->sidebar, TRUE);
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
  g_signal_connect(a->primary, "clicked", G_CALLBACK(on_primary), a);
  a->back = gtk_button_new_with_label("Back");
  gtk_widget_add_css_class(a->back, "nav");
  gtk_widget_set_visible(a->back, FALSE);
  g_signal_connect(a->back, "clicked", G_CALLBACK(on_back), a);
  gtk_box_append(GTK_BOX(header), a->back);
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
  gtk_box_append(GTK_BOX(overview), section_head("COMING LATER"));
  GtkWidget *note = gtk_label_new(
    "Charts are not drawn.\n"
    "Operations, projects, memory search and checked work are not in this window.\n"
    "Connecting an app waits: the core's list has no id, and this window does not keep its own list.\n"
    "macOS and Windows windows are not built.\n"
    "The lines above are the core's answers, read just now. No sample figures are shown.");
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

  GtkWidget *guide = gtk_box_new(GTK_ORIENTATION_VERTICAL, 16);
  a->guide_text = gtk_label_new("");
  gtk_widget_add_css_class(a->guide_text, "sub");
  gtk_label_set_xalign(GTK_LABEL(a->guide_text), 0);
  gtk_label_set_wrap(GTK_LABEL(a->guide_text), TRUE);
  gtk_label_set_max_width_chars(GTK_LABEL(a->guide_text), 64);
  a->guide_choices = gtk_box_new(GTK_ORIENTATION_VERTICAL, 8);
  a->choice_this = gtk_button_new_with_label("This computer");
  a->choice_other = gtk_button_new_with_label("Another computer");
  gtk_widget_add_css_class(a->choice_this, "nav");
  gtk_widget_add_css_class(a->choice_other, "nav");
  align_button_label(a->choice_this);
  align_button_label(a->choice_other);
  g_signal_connect(a->choice_this, "clicked", G_CALLBACK(guide_pick), a);
  g_signal_connect(a->choice_other, "clicked", G_CALLBACK(guide_pick), a);
  gtk_box_append(GTK_BOX(a->guide_choices), a->choice_this);
  gtk_box_append(GTK_BOX(a->guide_choices), a->choice_other);
  a->guide_entry = gtk_entry_new();
  gtk_widget_set_visible(a->guide_entry, FALSE);
  a->guide_apps = gtk_box_new(GTK_ORIENTATION_VERTICAL, 4);
  gtk_widget_set_visible(a->guide_apps, FALSE);
  a->guide_backup = gtk_check_button_new_with_label("Record this remote");
  gtk_widget_set_visible(a->guide_backup, FALSE);
  a->guide_detail = gtk_label_new("");
  gtk_label_set_xalign(GTK_LABEL(a->guide_detail), 0);
  gtk_label_set_wrap(GTK_LABEL(a->guide_detail), TRUE);
  gtk_label_set_max_width_chars(GTK_LABEL(a->guide_detail), 64);
  gtk_box_append(GTK_BOX(guide), a->guide_text);
  gtk_box_append(GTK_BOX(guide), a->guide_choices);
  gtk_box_append(GTK_BOX(guide), a->guide_entry);
  gtk_box_append(GTK_BOX(guide), a->guide_apps);
  gtk_box_append(GTK_BOX(guide), a->guide_backup);
  gtk_box_append(GTK_BOX(guide), a->guide_detail);

  gtk_stack_add_named(GTK_STACK(a->stack), guide, "guide");
  gtk_stack_add_named(GTK_STACK(a->stack), overview, "overview");
  gtk_stack_add_named(GTK_STACK(a->stack), health, "health");
  gtk_stack_add_named(GTK_STACK(a->stack), later, "later");
  gtk_box_append(GTK_BOX(page), a->stack);

  GtkWidget *scroll = gtk_scrolled_window_new();
  gtk_scrolled_window_set_policy(GTK_SCROLLED_WINDOW(scroll), GTK_POLICY_NEVER, GTK_POLICY_AUTOMATIC);
  gtk_scrolled_window_set_child(GTK_SCROLLED_WINDOW(scroll), page);
  gtk_widget_set_hexpand(scroll, TRUE);

  gtk_box_append(GTK_BOX(a->sidebar_column), a->sidebar);
  gtk_box_append(GTK_BOX(root), a->sidebar_column);
  gtk_box_append(GTK_BOX(root), edge);
  gtk_box_append(GTK_BOX(root), scroll);
  adw_application_window_set_content(ADW_APPLICATION_WINDOW(a->window), root);

  GSimpleAction *refresh = g_simple_action_new("refresh", NULL);
  g_signal_connect(refresh, "activate", G_CALLBACK(on_refresh), a);
  g_action_map_add_action(G_ACTION_MAP(a->window), G_ACTION(refresh));
  const char *accels[] = {"<Control>r", NULL};
  gtk_application_set_accels_for_action(application, "win.refresh", accels);

  reload_glance(a);
  guide_plan_clear(&a->plan);
  if (want_guide() && a->glance.have_binary) {
    guide_show(a, 0);
  } else {
    show_page(a, 0);
  }
  gtk_window_present(GTK_WINDOW(a->window));

  if (a->self_check) {
    /* Wait until the window has a real width, so the sidebar check is the allocated column. */
    g_timeout_add(200, report_and_quit, a);
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
