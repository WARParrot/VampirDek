using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Definitions;
using UnityEngine;
namespace Shared.Localization
{
    /// <summary>
    /// Serializable key + fallback value. Use this for future content so gameplay/data assets store
    /// stable localization keys while still remaining readable and safe when a translation is missing.
    /// </summary>
    [Serializable]
    public struct LocalizedString
    {
        public string Key;
        [TextArea(2, 6)] public string Fallback;
        public LocalizedString(string key, string fallback)
        {
            Key = key;
            Fallback = fallback;
        }
        public string Resolve()
        {
            return LocalizationService.T(Key, Fallback);
        }
    }
    [Serializable]
    public class LocalizedEntry
    {
        public string Key;
        [TextArea(1, 8)] public string Value;
    }
    /// <summary>
    /// Optional table asset for the production transition: designers/translators can move entries out of
    /// code into ScriptableObject/CSV-generated assets without changing call sites.
    /// </summary>
    [CreateAssetMenu(menuName = "Localization/Runtime Table")]
    public class RuntimeLocalizationTable : ScriptableObject
    {
        public string LanguageCode = "ru";
        public List<LocalizedEntry> Entries = new List<LocalizedEntry>();
        private void OnEnable()
        {
            LocalizationService.RegisterTable(LanguageCode, Entries);
        }
    }
    /// <summary>
    /// Thin project-local localization facade. It intentionally mirrors a production localization boundary:
    /// stable keys in content, formatting through templates, missing-key diagnostics, and pluggable tables.
    /// </summary>
    public static class LocalizationService
    {
        private const string PlayerPrefsLanguageKey = "locale.language";
        private const string DefaultLanguage = "ru";
        private const string FallbackLanguage = "en";
        private static readonly Dictionary<string, Dictionary<string, string>> Tables = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> MissingKeys = new HashSet<string>();
        private static bool _initialized;
        private static string _currentLanguage = DefaultLanguage;
        public static event Action<string> LanguageChanged;
        public static string CurrentLanguage
        {
            get
            {
                EnsureInitialized();
                return _currentLanguage;
            }
        }
        public static void SetLanguage(string languageCode)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(languageCode)) languageCode = DefaultLanguage;
            languageCode = languageCode.Trim().ToLowerInvariant();
            if (_currentLanguage == languageCode) return;
            _currentLanguage = languageCode;
            PlayerPrefs.SetString(PlayerPrefsLanguageKey, _currentLanguage);
            PlayerPrefs.Save();
            LanguageChanged?.Invoke(_currentLanguage);
        }
        public static void RegisterTable(string languageCode, IEnumerable<LocalizedEntry> entries)
        {
            if (string.IsNullOrWhiteSpace(languageCode) || entries == null) return;
            EnsureInitialized();
            var table = GetOrCreateTable(languageCode.Trim().ToLowerInvariant());
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key)) continue;
                table[NormalizeKey(entry.Key)] = entry.Value ?? string.Empty;
            }
        }
        public static void RegisterTable(string languageCode, IReadOnlyDictionary<string, string> entries)
        {
            if (string.IsNullOrWhiteSpace(languageCode) || entries == null) return;
            EnsureInitialized();
            var table = GetOrCreateTable(languageCode.Trim().ToLowerInvariant());
            foreach (var pair in entries)
            {
                if (string.IsNullOrWhiteSpace(pair.Key)) continue;
                table[NormalizeKey(pair.Key)] = pair.Value ?? string.Empty;
            }
        }
        public static string T(string key, string fallback = null)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(key)) return fallback ?? string.Empty;
            key = NormalizeKey(key);
            if (TryLookup(_currentLanguage, key, out var value)) return value;
            if (!string.Equals(_currentLanguage, FallbackLanguage, StringComparison.OrdinalIgnoreCase) && TryLookup(FallbackLanguage, key, out value)) return value;
            if (TryLookup(DefaultLanguage, key, out value)) return value;
            if (MissingKeys.Add(key)) Debug.LogWarning($"[Localization] Missing key '{key}' for language '{_currentLanguage}'.");
            return fallback ?? key;
        }
        public static string TFormat(string key, string fallback, params object[] args)
        {
            var template = T(key, fallback);
            if (args == null || args.Length == 0) return template;
            try
            {
                return string.Format(CultureInfo.InvariantCulture, template, args);
            }
            catch (FormatException ex)
            {
                Debug.LogWarning($"[Localization] Bad format for key '{key}': {ex.Message}");
                return template;
            }
        }
        public static string CardName(CardDef def)
        {
            if (def == null) return T("ui.card.default", "Card");
            var fallback = string.IsNullOrWhiteSpace(def.CardName) ? def.name : def.CardName;
            var key = FirstNonEmpty(def.CardNameKey, KeyFromName("card", def.name, "name"), KeyFromName("card", fallback, "name"));
            return T(key, fallback);
        }
        public static string EnchantmentName(EnchantmentData data)
        {
            if (data == null) return T("ui.passive.default", "Passive");
            var fallback = string.IsNullOrWhiteSpace(data.DisplayName) ? data.name : data.DisplayName;
            var key = FirstNonEmpty(data.DisplayNameKey, KeyFromName("enchantment", data.name, "name"), KeyFromName("enchantment", fallback, "name"));
            return T(key, fallback);
        }
        public static string HintMessage(HintData hint)
        {
            if (hint == null) return string.Empty;
            var key = FirstNonEmpty(hint.MessageKey, KeyFromName("hint", hint.name, "message"));
            return T(key, hint.Message);
        }
        public static string RowTypeName(Definitions.RowType rowType)
        {
            return T("row." + rowType.ToString().ToLowerInvariant(), rowType.ToString());
        }
        public static string ShortRowTypeName(Definitions.RowType rowType)
        {
            return T("row.short." + rowType.ToString().ToLowerInvariant(), RowTypeName(rowType));
        }
        public static string StatName(string stat)
        {
            if (string.IsNullOrWhiteSpace(stat)) return T("stat.default", "Stat");
            return T("stat." + SafeKey(stat), stat);
        }
        public static string KeyFromName(string prefix, string name, string suffix)
        {
            var safe = SafeKey(name);
            if (string.IsNullOrEmpty(safe)) return string.Empty;
            return string.IsNullOrWhiteSpace(suffix) ? $"{prefix}.{safe}" : $"{prefix}.{safe}.{suffix}";
        }
        public static string SafeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var builder = new StringBuilder(value.Length);
            foreach (var c in value.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) builder.Append(c);
                else if (c == '.' || c == '_' || c == '-') builder.Append(c);
                else if (char.IsWhiteSpace(c)) builder.Append('_');
            }
            return builder.ToString().Trim('_');
        }
        public static string FirstNonEmpty(params string[] values)
        {
            if (values == null) return string.Empty;
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
            return string.Empty;
        }
        private static void EnsureInitialized(bool registerBuiltIns = true)
        {
            if (_initialized) return;
            _initialized = true;
            _currentLanguage = PlayerPrefs.GetString(PlayerPrefsLanguageKey, DefaultLanguage).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(_currentLanguage)) _currentLanguage = DefaultLanguage;
            if (registerBuiltIns) RegisterBuiltInTables();
        }
        private static Dictionary<string, string> GetOrCreateTable(string languageCode)
        {
            languageCode = string.IsNullOrWhiteSpace(languageCode) ? DefaultLanguage : languageCode.Trim().ToLowerInvariant();
            if (!Tables.TryGetValue(languageCode, out var table))
            {
                table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Tables[languageCode] = table;
            }
            return table;
        }
        private static bool TryLookup(string languageCode, string key, out string value)
        {
            value = null;
            return Tables.TryGetValue(languageCode, out var table) && table.TryGetValue(key, out value) && value != null;
        }
        private static string NormalizeKey(string key)
        {
            return key.Trim();
        }
        private static void RegisterBuiltInTables()
        {
            RegisterTable("ru", new Dictionary<string, string>
            {
                ["card.town.name"] = "Город",
                ["card.basetown.name"] = "Город",
                ["card.human.name"] = "Человек",
                ["card.building.name"] = "Здание",
                ["card.usualbuilding.name"] = "Здание",
                ["card.vampire.name"] = "Вампир",
                ["card.vampirefodder.name"] = "Вампир",
                ["card.ghoul.name"] = "Гуль",
                ["card.bloodwitch.name"] = "Кровавая колдунья",
                ["card.nightfury.name"] = "Ночная фурия",
                ["card.vampireloner.name"] = "Вампир-одиночка",
                ["card.freshspawn.name"] = "Молодняк",
                ["card.ritualist.name"] = "Ритуальщик",
                ["card.decoy.name"] = "Приманка",
                ["card.gourmet.name"] = "Гурман",
                ["card.bloodaltar.name"] = "Кровавый алтарь",
                ["card.crypt.name"] = "Склеп",
                ["enchantment.shield.name"] = "Щит",
                ["enchantment.attack.name"] = "Атака",
                ["enchantment.humanresourceincrease1.name"] = "Прирост HR +1",
                ["enchantment.enchantmentattack.name"] = "Щит",
                ["enchantment.enchantmentshileld.name"] = "Атака",
                ["enchantment.increasehrenchantment.name"] = "Прирост HR +1",
                ["row.vanguard"] = "Авангард",
                ["row.building"] = "Здание",
                ["row.human"] = "Люди",
                ["row.town"] = "Город",
                ["row.short.vanguard"] = "Авангард",
                ["row.short.building"] = "Здание",
                ["row.short.human"] = "Люди",
                ["row.short.town"] = "Город",
                ["stat.default"] = "Стат",
                ["stat.attack"] = "Атака",
                ["stat.health"] = "Здоровье",
                ["stat.speed"] = "Скорость",
                ["stat.humanresources"] = "HR",
                ["ui.card.default"] = "Карта",
                ["ui.empty"] = "Пусто",
                ["ui.card_details.title"] = "Детали карты",
                ["ui.card_details.close_hint"] = "Нажмите в любом месте, чтобы закрыть",
                ["ui.town_hp"] = "Город: {0}",
                ["ui.opponent_town_hp"] = "Город врага: {0}",
                ["ui.hr"] = "HR: {0}",
                ["ui.cost.free"] = "Стоимость: бесплатно",
                ["ui.cost.line"] = "Стоимость: {0}",
                ["ui.cost.hr"] = "{0} HR",
                ["ui.cost.human.single"] = "Человек",
                ["ui.cost.human.many"] = "{0} чел.",
                ["ui.cost.sacrifice.single"] = "Жертва: {0}",
                ["ui.cost.sacrifice.many"] = "Жертва: {0} x{1}",
                ["ui.card.hp"] = "HP {0}",
                ["ui.card.hp_full"] = "HP: {0}/{1}",
                ["ui.card.attack"] = "Атака: {0}",
                ["ui.card.attack_short"] = "АТК {0}",
                ["ui.card.speed_short"] = "СКР {0}",
                ["ui.card.speed_roll"] = "Бросок скорости: {0}",
                ["ui.card.speed"] = "Скорость: {0}",
                ["ui.card.speed_roll_current"] = "{0} (бросок {1})",
                ["ui.card.target"] = "Цель: {0}",
                ["ui.card.passives"] = "Пассивы:",
                ["ui.card.passives_none"] = "Пассивы: нет",
                ["ui.passive.default"] = "Пассив",
                ["ui.passive.detail"] = "{0}: {1}",
                ["ui.modifier.add"] = "{0} {1}",
                ["ui.modifier.multiply"] = "{0} x{1}",
                ["ui.trigger.on"] = "при {0}",
                ["ui.duration.turns"] = "{0} ход.",
                ["deck.location.deck"] = "Колода: {0}",
                ["deck.location.hand"] = "Рука: {0}",
                ["deck.location.discard"] = "Сброс: {0}",
                ["deck.stats"] = "Всего карт: {0} | В колоде: {1} | В руке: {2} | В сбросе: {3}",
                ["deck.row.unknown"] = "Неизвестно",
                ["phase.building"] = "Строительство",
                ["phase.planning"] = "Планирование",
                ["phase.clashing"] = "Столкновение",
                ["phase.one_sided_attack"] = "Атака",
                ["phase.start_of_turn"] = "Начало хода",
                ["phase.end_of_turn"] = "Конец хода",
                ["phase.duel_start"] = "Начало дуэли",
                ["phase.loot"] = "Награда",
                ["phase.duel_end"] = "Конец дуэли",
                ["phase.confirm_button"] = "Подтвердить",
                ["menu.paused"] = "Пауза",
                ["menu.resume"] = "Продолжить",
                ["menu.exit_save"] = "Сохранить и выйти",
                ["interaction.default_prompt"] = "Нажмите [E], чтобы взаимодействовать",
                ["interaction.open"] = "Открыть",
                ["interaction.close"] = "Закрыть",
                ["interaction.hack"] = "Взломать",
                ["interaction.enter_world"] = "Войти: {0}",
                ["interaction.start_duel"] = "Нажмите [E], чтобы начать дуэль",
                ["tutorial.exploration_intro"] = "Исследование — пауза между дуэлями. Здесь вы управляете рисками: бой, награда, отступление.",
                ["tutorial.exploration_move_intro"] = "Сначала освойтесь. Двери, встречи и тайники важны только если вы можете до них добраться.",
                ["tutorial.exploration_move_prompt"] = "Двигайтесь клавишами WASD или стрелками.",
                ["tutorial.exploration_look_intro"] = "Осматривайтесь: так вы находите объекты, пути, опасности и встречи.",
                ["tutorial.exploration_look_prompt"] = "Поверните камеру мышью.",
                ["tutorial.exploration_interact_intro"] = "Когда видите подсказку, взаимодействуйте: так открываются объекты и пути перед тем, как ввязаться в дуэль.",
                ["tutorial.exploration_interact_prompt"] = "Посмотрите на интерактивный объект и нажмите E.",
                ["tutorial.exploration_deck_intro"] = "Перед опасностью проверьте колоду: это ваш план выживания в следующей дуэли.",
                ["tutorial.exploration_deck_prompt"] = "Нажмите Esc, чтобы открыть меню и посмотреть текущую колоду.",
                ["tutorial.exploration_outro"] = "Исследование: осмотритесь, проверьте колоду и выбирайте бой. Любопытство строит игру; спешка может её закончить.",
                ["tutorial.hand_initializing"] = "Туториал ожидает инициализацию руки.",
                ["tutorial.playable_card_hint"] = "Возьмите {0} ({1}) и перетащите в свободный слот ряда {2}.",
                ["tutorial.no_playable_card"] = "Сейчас нет подходящей карты для этого шага (HR={0}). Шаг будет пропущен.",
                ["tutorial.cost.free"] = "без стоимости",
                ["warning.not_enough_mana"] = "Нельзя использовать — не хватает маны\nТребуется: {0}, Доступно: {1}",
                ["warning.not_enough_hr"] = "Нельзя использовать — не хватает HR\nТребуется: {0}, Доступно: {1}",
                ["warning.need_sacrifice"] = "Нельзя использовать — нужна жертва\nТребуется: {0} {1}, Доступно: {2}",
                ["warning.not_enough_resources"] = "Нельзя использовать — недостаточно ресурсов\n{0}",
                ["novel.start.text"] = "Нажмите E, чтобы взаимодействовать",
                ["novel.start_1.text"] = "Подойдите к столу и нажмите E, чтобы начать дуэль",
                ["novel.start_2.text"] = "Карты можно перетаскивать на поле. У некоторых карт стоимость указана в правом верхнем углу. Характеристики карты появятся на ней после размещения.",
                ["novel.start_3.text"] = "Карты можно выкладывать только в фазе строительства. Атаки назначаются в фазе планирования: сначала нажмите атакующую карту, затем цель.",
                ["novel.start_4.text"] = "Не забывайте осматриваться вокруг в поисках интерактивных объектов.",
                ["speaker.viven.name"] = "Вивен",
                ["hint.planningphaseenter.message"] = "Выбирай кого атаковать",
                ["hint.vanguardinhandhint.message"] = "В руке есть карта Авангарда",
                ["tutorial.duel_intro"] = "Первая дуэль. Задача простая: защитить свой город и разрушить город противника.",
                ["tutorial.hr_intro"] = "HR — это запас людей. Чаще всего он нужен для зданий.\n\nВ начале хода HR становится равен числу живых карт в вашем ряду Люди.",
                ["tutorial.play_human_drag"] = "Фаза строительства: выкладываем карты на поле.\n\nСначала сыграйте Человека. Человек пригодится как жертва для Вампира и увеличит HR на следующем ходу.\n\n{PlayableCardHint}",
                ["tutorial.play_human_place"] = "Положите Человека в подсвеченный слот ряда Люди.\n\n{PlayableCardHint}",
                ["tutorial.play_vampire_drag"] = "Теперь сыграйте Вампира. Это боец Авангарда: основного атакующего ряда.\n\nКарты Авангарда часто требуют жертвовать Людей.\n\n{PlayableCardHint}",
                ["tutorial.play_vampire_place"] = "Положите Вампира в подсвеченный слот Авангарда.\n\n{PlayableCardHint}\n\nРяды поля:\n• Авангард — атакующие карты\n• Здания — обычно играются за HR\n• Люди — для HR, эффектов и жертв",
                ["tutorial.confirm_building_intro"] = "Отлично: на поле есть атакующий Вампир. Дальше подтвердим строительство и выберем цель атаки.",
                ["tutorial.confirm_building"] = "Нажмите подтверждение фазы, чтобы перейти к планированию атак.",
                ["tutorial.planning_intro"] = "Фаза планирования: здесь назначаются атаки.\n\nЗажмите своего живого бойца с АТК > 0 — например Вампира в Авангарде — и протяните линию к цели.",
                ["tutorial.select_target"] = "Отпустите на живой карте противника. Выбранный Вампир будет атаковать её в бою.",
                ["tutorial.confirm_planning"] = "Цель выбрана. Подтвердите фазу планирования — дальше бой разыграется автоматически.",
                ["tutorial.buildings_shield"] = "Здания работают как щит. Пока у противника живо хотя бы одно здание — оно прикрывает двух людей за собой. Город пока бить можно. Но если у противника живы оба здания, город становится недосягаем: сначала придётся снести хотя бы одно из них.",
                ["tutorial.clash_intro"] = "Бой: если две карты атакуют друг друга, происходит столкновение. Урон считается автоматически.",
                ["tutorial.one_sided_attack"] = "Если атака не встречена ответной атакой, карта просто наносит урон выбранной цели.",
                ["tutorial.turn_end"] = "Ход почти закончен. На следующем ходу HR обновится по числу живых Людей, игроки возьмут карты, а временный урон зданий сбросится.",
                ["tutorial.leave_duel"] = "Основы дуэли разобраны. Теперь можно отойти от стола и осмотреться вокруг — бой сохранится.\n\nЧтобы открыть дальнейший путь, вернитесь к столу и доведите дуэль до победы: разрушьте город противника.\n\nНажмите S, чтобы покинуть стол. Когда будете готовы продолжить бой, подойдите к нему снова и нажмите E."
            });
            RegisterTable("en", new Dictionary<string, string>
            {
                ["card.town.name"] = "Town",
                ["card.basetown.name"] = "Town",
                ["card.human.name"] = "Human",
                ["card.building.name"] = "Building",
                ["card.usualbuilding.name"] = "Building",
                ["card.vampire.name"] = "Vampire",
                ["card.vampirefodder.name"] = "Vampire",
                ["enchantment.shield.name"] = "Shield",
                ["enchantment.attack.name"] = "Attack",
                ["enchantment.humanresourceincrease1.name"] = "Human resource +1",
                ["enchantment.enchantmentattack.name"] = "Shield",
                ["enchantment.enchantmentshileld.name"] = "Attack",
                ["enchantment.increasehrenchantment.name"] = "Human resource +1",
                ["row.vanguard"] = "Vanguard",
                ["row.building"] = "Building",
                ["row.human"] = "Human",
                ["row.town"] = "Town",
                ["row.short.vanguard"] = "Vanguard",
                ["row.short.building"] = "Building",
                ["row.short.human"] = "Human",
                ["row.short.town"] = "Town",
                ["stat.default"] = "Stat",
                ["stat.attack"] = "Attack",
                ["stat.health"] = "Health",
                ["stat.speed"] = "Speed",
                ["stat.humanresources"] = "HR",
                ["ui.card.default"] = "Card",
                ["ui.empty"] = "Empty",
                ["ui.card_details.title"] = "Card details",
                ["ui.card_details.close_hint"] = "Click anywhere to close",
                ["ui.town_hp"] = "Town HP: {0}",
                ["ui.opponent_town_hp"] = "Opp Town HP: {0}",
                ["ui.hr"] = "HR: {0}",
                ["ui.cost.free"] = "Cost: free",
                ["ui.cost.line"] = "Cost: {0}",
                ["ui.cost.mana"] = "{0} mana",
                ["ui.cost.hr"] = "{0} HR",
                ["ui.cost.human.single"] = "Human",
                ["ui.cost.human.many"] = "{0} Human",
                ["ui.cost.sacrifice.single"] = "Sacrifice one {0}",
                ["ui.cost.sacrifice.many"] = "Sacrifice {1} {0} cards",
                ["ui.card.hp"] = "HP {0}",
                ["ui.card.hp_full"] = "HP: {0}/{1}",
                ["ui.card.attack"] = "Attack: {0}",
                ["ui.card.attack_short"] = "ATK {0}",
                ["ui.card.speed_short"] = "SPD {0}",
                ["ui.card.speed_roll"] = "Speed roll: {0}",
                ["ui.card.speed"] = "Speed: {0}",
                ["ui.card.speed_roll_current"] = "{0} (roll {1})",
                ["ui.card.target"] = "Target: {0}",
                ["ui.card.passives"] = "Passives:",
                ["ui.card.passives_none"] = "Passives: none",
                ["ui.passive.default"] = "Passive",
                ["ui.passive.detail"] = "{0}: {1}",
                ["ui.modifier.add"] = "{0} {1}",
                ["ui.modifier.multiply"] = "{0} x{1}",
                ["ui.trigger.on"] = "on {0}",
                ["ui.duration.turns"] = "{0} turns",
                ["deck.location.deck"] = "Deck: {0}",
                ["deck.location.hand"] = "Hand: {0}",
                ["deck.location.discard"] = "Discard: {0}",
                ["deck.stats"] = "Total cards: {0} | In deck: {1} | In hand: {2} | Discard: {3}",
                ["deck.row.unknown"] = "Unknown",
                ["phase.building"] = "Building",
                ["phase.planning"] = "Planning",
                ["phase.clashing"] = "Clash",
                ["phase.one_sided_attack"] = "Attack",
                ["phase.start_of_turn"] = "Start of turn",
                ["phase.end_of_turn"] = "End of turn",
                ["phase.duel_start"] = "Duel start",
                ["phase.loot"] = "Reward",
                ["phase.duel_end"] = "Duel end",
                ["phase.confirm_button"] = "Confirm",
                ["menu.paused"] = "Paused",
                ["menu.resume"] = "Resume",
                ["menu.exit_save"] = "Exit & Save",
                ["interaction.default_prompt"] = "Press [E] to interact",
                ["interaction.open"] = "Open",
                ["interaction.close"] = "Close",
                ["interaction.hack"] = "Hack",
                ["interaction.enter_world"] = "Enter {0}",
                ["interaction.start_duel"] = "Press [E] to start duel",
                ["tutorial.exploration_intro"] = "Exploration is where the run breathes. Between duels, you decide what is worth risking: a fight, a reward, or a retreat.",
                ["tutorial.exploration_move_intro"] = "First, get your bearings. Doors, encounters, secrets, and escape routes only matter once you can reach them.",
                ["tutorial.exploration_move_prompt"] = "Move with WASD or the arrow keys.",
                ["tutorial.exploration_look_intro"] = "Your gaze is how you ask the world questions. Look around to find what can be inspected, used, avoided, or fought.",
                ["tutorial.exploration_look_prompt"] = "Move the mouse to look around.",
                ["tutorial.exploration_interact_intro"] = "When the world answers, interact. This is how you inspect objects and open paths before you commit to a duel.",
                ["tutorial.exploration_interact_prompt"] = "Face an interactable prompt, then press E.",
                ["tutorial.exploration_deck_intro"] = "Before you accept danger, check what you are carrying. Your deck is not a menu footnote; it is your plan for surviving the next duel.",
                ["tutorial.exploration_deck_prompt"] = "Press Esc to open the menu and review your current deck.",
                ["tutorial.exploration_outro"] = "That is Exploration: read the room, check the deck, then choose the fight. Curiosity builds the run; haste can end it.",
                ["tutorial.hand_initializing"] = "Tutorial is waiting for the hand to initialize.",
                ["tutorial.playable_card_hint"] = "Take {0} ({1}) and drag it into a free {2} row slot.",
                ["tutorial.no_playable_card"] = "There is no playable card for this step right now (HR={0}). The step will be skipped.",
                ["tutorial.cost.free"] = "free",
                ["warning.not_enough_mana"] = "Cannot play — not enough mana\nRequired: {0}, Available: {1}",
                ["warning.not_enough_hr"] = "Cannot play — not enough HR\nRequired: {0}, Available: {1}",
                ["warning.need_sacrifice"] = "Cannot play — sacrifice required\nRequired: {0} {1}, Available: {2}",
                ["warning.not_enough_resources"] = "Cannot play — not enough resources\n{0}",
                ["novel.start.text"] = "Press E to interact",
                ["novel.start_1.text"] = "Walking up to a table and pressing E starts the duel",
                ["novel.start_2.text"] = "Cards can be dragged and droppend onto the board, some of them have cost defined in the right upper corner. Stats of the card are written on it once placed.",
                ["novel.start_3.text"] = "Cards can only be placed in thebuilding phase, while attacks are assigned by pressing on an attacking card and then on a target card during the planning phase.",
                ["novel.start_4.text"] = "Don't forget to look around for interactable objects.",
                ["speaker.viven.name"] = "Viven",
                ["hint.planningphaseenter.message"] = "Choose who to attack",
                ["hint.vanguardinhandhint.message"] = "Vanguard card in hand",
                ["tutorial.duel_intro"] = "First duel. The task is simple: defend your town and destroy the enemy town.",
                ["tutorial.hr_intro"] = "HR is your pool of people. It is most often needed for buildings.\n\nAt the start of a turn, HR becomes equal to the number of living cards in your Human row.",
                ["tutorial.play_human_drag"] = "Building Phase: play cards onto the board.\n\nFirst play Human. Human can be sacrificed for Vampire and increases HR next turn.\n\n{PlayableCardHint}",
                ["tutorial.play_human_place"] = "Place Human into the highlighted Human row slot.\n\n{PlayableCardHint}",
                ["tutorial.play_vampire_drag"] = "Now play Vampire. It is a Vanguard fighter: the main attacking row.\n\nVanguard cards often require sacrificing Human cards.\n\n{PlayableCardHint}",
                ["tutorial.play_vampire_place"] = "Place Vampire into the highlighted Vanguard slot.\n\n{PlayableCardHint}\n\nBoard rows:\n• Vanguard — attacking cards\n• Building — buildings, usually played for HR\n• Human — people for HR, effects, and sacrifices",
                ["tutorial.confirm_building_intro"] = "Great: you have a Human and an attacking Vampire on the board. Next, confirm building and choose an attack target.",
                ["tutorial.confirm_building"] = "Press phase confirm to move to attack planning.",
                ["tutorial.planning_intro"] = "Planning Phase: assign attacks here.\n\nDrag from your living fighter with ATK > 0 — for example, Vampire in Vanguard — to the target.",
                ["tutorial.select_target"] = "Release on a living enemy card. The selected Vampire will attack it in combat.",
                ["tutorial.confirm_planning"] = "Target selected. Confirm Planning Phase — combat will resolve automatically.",
                ["tutorial.clash_intro"] = "Combat: if two cards attack each other, they clash. Damage is calculated automatically.",
                ["tutorial.one_sided_attack"] = "If an attack is not answered by a counterattack, the card simply deals damage to the selected target.",
                ["tutorial.turn_end"] = "The turn is almost over. Next turn, HR refreshes from living Humans, players draw cards, and temporary building damage resets.",
                ["tutorial.leave_duel"] = "Duel basics complete. You can step away from the table and look around — the fight will be saved.\n\nTo open the next path, return to the table and finish the duel by destroying the enemy town.\n\nPress S to leave the table. When you are ready to continue, approach it again and press E."
            });
        }
    }
}
