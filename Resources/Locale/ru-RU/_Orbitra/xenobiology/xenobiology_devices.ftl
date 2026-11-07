# SPDX-License-Identifier: MIT
# Copyright (c) 2026 Ende (Orbitra)

orbitra-xeno-title = Лаборатория ксенобиологии
orbitra-xeno-specimens = Образцы
orbitra-xeno-catalog = Справочник пород
orbitra-xeno-count = Образцов в радиусе: { $count }
orbitra-xeno-unpowered = Нет питания. Измерения недоступны.
orbitra-xeno-process = Получить экстракты
orbitra-xeno-eject = Вернуть особь
orbitra-xeno-search = Поиск породы...
orbitra-xeno-captured = Образец помещён в переносчик.
orbitra-xeno-baby = детёныш
orbitra-xeno-adult = взрослый
orbitra-xeno-dead = мёртвый
orbitra-xeno-specimen = [bold]{ $breed }[/bold] | { $stage } | Поколение { $generation }
    Рост: { $growth }% | Сытость: { $hunger }% | Биомасса: { $biomass } | Тело: { $temperature } °C
    Мутация: { $mutation }% | Потомство: { $offspring } | Экстракты: { $yield }
    Скорость роста: ×{ $rate } | Покладистость: { $docility }% | Термостойкость: +{ $tolerance } K
orbitra-xeno-guide = Кормите настоящей едой и регулируйте температуру воздуха в загоне. Взрослого слайма можно разделить через контекстное меню. Мёртвых взрослых особей перерабатывайте в экстракты, вводите указанный реагент. Для выведения культуры добавьте 5 ед. воды и положите капсулу на пол.
orbitra-xeno-breed-button = { $tier }: { $name }
orbitra-xeno-breed-conditions = Рост: { $seconds } с; биомасса для взросления: { $growth }; для деления: { $division }. Комфортная температура тела: от { $minimum } до { $maximum } °C.
orbitra-xeno-mutations = [bold]Возможные мутации потомства[/bold]
orbitra-xeno-stable-lineage = Стабильная конечная порода.
orbitra-xeno-reactions = [bold]Одноразовые реакции экстракта[/bold]
orbitra-xeno-reaction = [bold]{ $name }[/bold] | { $amount } ед. { $reagent }
    { $description }
research-technology-orbitra-xenobiology = Ксенобиология
research-technology-orbitra-colony-engineering = Инженерия колонии
orbitra-research-technology-orbitra-xenobiology-description = Оборудование для разведения лабораторных слаймов, наблюдения за наследуемыми свойствами и получения одноразовых экстрактов.
orbitra-research-technology-orbitra-colony-engineering-description = Вместительные переносчики и термостаты для управления условиями роста и селекции колонии.
ent-OrbitraXenoProcessor = процессор слаймов
    .desc = Извлекает экстракты из трупов взрослых лабораторных слаймов. Перенесите труп переносчиком и запустите обработку.
    .suffix = Orbitra
ent-OrbitraXenoConsole = консоль ксенобиологии
    .desc = Измеряет параметры слаймов поблизости и содержит справочник мутаций и реакций экстрактов.
    .suffix = Orbitra
ent-OrbitraXenoThermostat = лабораторный термостат
    .desc = Нагревает или охлаждает воздух в загоне. Температура тела слаймов меняется постепенно при теплообмене.
    .suffix = Orbitra
ent-OrbitraXenoCarrier = переносчик слаймов
    .desc = При использовании на слайме захватывает его. Активируйте в руке для выпуска или примените к процессору для переноса трупа взрослой особи.
    .suffix = Orbitra
ent-OrbitraXenoCarrierAdvanced = переносчик колонии
    .desc = Вмещает трёх лабораторных слаймов. Сначала выпускает или переносит пойманный раньше остальных образец.
    .suffix = Orbitra
ent-OrbitraXenoProcessorBoard = плата процессора слаймов
    .desc = Машинная плата процессора слаймов.
    .suffix = Orbitra
ent-OrbitraXenoConsoleBoard = плата консоли ксенобиологии
    .desc = Плата консоли ксенобиологии.
    .suffix = Orbitra
ent-OrbitraXenoThermostatBoard = плата лабораторного термостата
    .desc = Машинная плата лабораторного термостата.
    .suffix = Orbitra
ent-OrbitraXenoStarterCrate = ящик ксенобиологии
    .desc = Стартовые культуры, переносчик и набор для кормления колонии.
    .suffix = Orbitra
orbitra-xeno-identity = { $name } №{ $identifier }
orbitra-xeno-list-status = { $status } · Поколение { $generation }
orbitra-xeno-live-count = Измерения · Особей: { $count } · Радиус: { $range } тайлов
orbitra-xeno-specimen-search = Имя, номер или порода...
orbitra-xeno-filter-all = Все особи
orbitra-xeno-filter-baby = Живые детёныши
orbitra-xeno-filter-adult = Живые взрослые
orbitra-xeno-filter-attention = Требуют внимания
orbitra-xeno-no-specimens = В радиусе сканирования нет особей.
orbitra-xeno-no-matching-specimens = По вашему запросу ничего не найдено.
orbitra-xeno-no-breeds = По вашему запросу породы не найдены.
orbitra-xeno-select-specimen = Выберите особь для просмотра её состояния.
orbitra-xeno-colony-start-hint = Выведите слайма из стартовой культуры и держите рядом с консолью. Здесь появятся его состояние и наследственные признаки. Условия роста и реакции экстрактов доступны в справочнике пород.
orbitra-xeno-search-hint = Измените поисковый запрос или выберите «Все особи», чтобы вернуть скрытых особей в список.
orbitra-xeno-offline-hint = Восстановите питание для измерения состояния колонии. Справочник пород доступен без питания.
orbitra-xeno-back-to-list = К списку
orbitra-xeno-open-breed = Справка о породе
orbitra-xeno-selected-breed = { $breed } · Поколение { $generation }
orbitra-xeno-contained = В контейнере; рост приостановлен
orbitra-xeno-hostile = Агрессивен
orbitra-xeno-cold = Слишком холодно; рост приостановлен
orbitra-xeno-hot = Слишком жарко; рост приостановлен
orbitra-xeno-hungry = Голоден; рост приостановлен
orbitra-xeno-growth-adult = Рост: взрослая особь
orbitra-xeno-growth-value = Рост: { $amount }%
orbitra-xeno-hunger-value = Сытость: { $amount }%
orbitra-xeno-biomass-value = Биомасса: { $amount } / { $required }
orbitra-xeno-temperature-value = Температура тела: { $temperature } °C
orbitra-xeno-temperature-range = Комфортный диапазон: { $minimum }...{ $maximum } °C
orbitra-xeno-genetics = Наследственные признаки
orbitra-xeno-gene-mutation = Шанс мутации
orbitra-xeno-gene-offspring = Потомков при делении
orbitra-xeno-gene-yield = Выход экстрактов
orbitra-xeno-gene-rate = Скорость роста
orbitra-xeno-gene-docility = Покладистость
orbitra-xeno-gene-tolerance = Доп. термостойкость
orbitra-xeno-percent = { $amount }%
orbitra-xeno-multiplier = ×{ $amount }
orbitra-xeno-kelvin = +{ $amount } K
orbitra-xeno-colony-summary = Показано { $visible } / { $total } · Требуют внимания: { $attention }
orbitra-xeno-catalog-count = Пород: { $count }
orbitra-xeno-help = Краткая справка
orbitra-xeno-tier = Уровень мутаций: { $tier }
orbitra-xeno-tier-all = Все уровни мутаций
orbitra-xeno-catalog-care = Уход и мутации
orbitra-xeno-catalog-recipes = Рецепты экстрактов
orbitra-xeno-care = Условия роста
orbitra-xeno-care-duration = Время взросления
orbitra-xeno-care-growth-biomass = Биомасса для взросления
orbitra-xeno-care-division-biomass = Биомасса для деления
orbitra-xeno-care-temperature = Температура тела
orbitra-xeno-care-temperature-value = { $minimum }...{ $maximum } °C
orbitra-xeno-seconds = { $seconds } с
orbitra-xeno-care-thresholds = Для роста нужно не менее 25% сытости, для деления не менее 50%. В контейнерах рост приостанавливается. Наследственные признаки меняют скорость роста и комфортный диапазон температур.
orbitra-xeno-mutation-heading = Пути мутаций
orbitra-xeno-mutation-explanation = Доли ниже действуют только при мутации потомка. Наследственный шанс родителя определяет, как часто она возникает.
orbitra-xeno-mutation-target = { $name } · { $chance }% мутаций
orbitra-xeno-reaction-heading = Рецепты экстрактов
orbitra-xeno-extract-instruction = Однократно введите указанное количество реагента в экстракт. Избыток реагента не увеличивает результат.
orbitra-xeno-catalyst = Нужно: { $amount } ед. { $reagent }
orbitra-xeno-product = { $count } × { $name }
orbitra-xeno-reagent-product = Образец: { $amount } ед. { $name }
orbitra-xeno-processor-title = Процессор слаймов
orbitra-xeno-processor-no-power = Нет питания. Особь можно извлечь.
orbitra-xeno-processor-ready = Готов к извлечению экстрактов
orbitra-xeno-processor-processing = Извлечение экстрактов
orbitra-xeno-processor-invalid = Нужен необработанный труп взрослого слайма
orbitra-xeno-processor-idle = Ожидание особи
orbitra-xeno-input-empty = Загрузочная камера пуста
orbitra-xeno-input-unknown = Неизвестная особь
orbitra-xeno-processor-instructions =
    1. Захватите труп взрослого слайма переносчиком.
    2. Примените загруженный переносчик к процессору.
    3. Нажмите «Получить экстракты».
orbitra-xeno-processor-consume-hint = Обработка займёт { $seconds } с и поглотит труп. До завершения особь можно вернуть.
orbitra-xeno-processor-cancel-hint = «Отменить и вернуть» остановит обработку и вернёт особь без расходования трупа.
orbitra-xeno-processor-power-hint = Для обработки нужно питание. Особь можно вернуть сейчас.
orbitra-xeno-processor-invalid-hint = Верните особь и загрузите необработанный труп взрослого слайма с помощью переносчика.
orbitra-xeno-cancel-eject = Отменить и вернуть
orbitra-xeno-expected-yield = Ожидаемый выход: { $count ->
    [one] { $count } экстракт
    [few] { $count } экстракта
   *[other] { $count } экстрактов
    }
orbitra-xeno-time-remaining = Осталось: { $seconds } с
orbitra-xeno-processor-finishing = Завершение обработки...
