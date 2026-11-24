ent-OrbitraAtm = банкомат Orbitra
    .desc = Терминал для управления личным счётом. Принимает и выдаёт кредиты.
ent-OrbitraAtmBoard = плата банкомата
    .desc = Печатная плата для банкомата Orbitra.
ent-OrbitraAtmKit = набор для сборки банкомата
    .desc = Плата и материалы для сборки банкомата.

orbitra-atm-title = Банкомат Orbitra
orbitra-atm-account = Личный счёт №{ $account }
orbitra-atm-balance =
    { $amount ->
        [one] { $amount } кредит
        [few] { $amount } кредита
       *[other] { $amount } кредитов
    }
orbitra-atm-id-slot = ID-карта
orbitra-atm-insert-card = Вставить ID
orbitra-atm-eject-card = Забрать ID
orbitra-atm-login = Возьмите свою ID-карту в активную руку и вставьте её в банкомат.
orbitra-atm-card-occupied = В банкомате уже есть ID-карта.
orbitra-atm-card-insert-denied = Возьмите свою ID-карту в активную руку. Слот должен быть свободен.
orbitra-atm-card-eject-denied = Забрать свою ID-карту не удалось.
orbitra-atm-amount = Сумма в кредитах
orbitra-atm-recipient = Номер счёта получателя
orbitra-atm-withdraw = Снять
orbitra-atm-transfer = Перевести
orbitra-atm-deposit = Внести наличные из руки
orbitra-atm-history = Последние операции
orbitra-atm-entry-start = Стартовый баланс · { $amount }
orbitra-atm-entry-salary = Зарплата · { $amount }
orbitra-atm-entry-deposit = Внесение наличных · { $amount }
orbitra-atm-entry-withdraw = Снятие наличных · { $amount }
orbitra-atm-entry-transfer = Перевод, счёт №{ $account } · { $amount }
orbitra-atm-entry-purchase = Покупка · { $amount }
orbitra-atm-entry-unknown = Операция · { $amount }
orbitra-atm-deposit-denied = Вставьте свою ID-карту и проверьте наличные в активной руке и лимит счёта.
orbitra-atm-withdraw-denied = Вставьте свою ID-карту и проверьте баланс.
orbitra-atm-transfer-denied = Вставьте свою ID-карту и проверьте сумму и номер счёта получателя.
orbitra-payroll-notification-title = Зарплата
orbitra-payroll-notification = На ваш счёт зачислено { orbitra-atm-balance }.
orbitra-retail-vending-price = { $price } кр.
orbitra-retail-payment-denied = Оплата не прошла. Нужно { $price } кр. на счёте или в активной руке.
