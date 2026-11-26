ent-OrbitraAtm = ATM
    .desc = A terminal for personal accounts. It accepts and dispenses physical credits.
ent-OrbitraAtmBoard = ATM circuit board
    .desc = A circuit board for an ATM.
ent-OrbitraAtmKit = ATM kit
    .desc = A terminal board and materials to assemble it.

orbitra-atm-title = ATM
orbitra-atm-account = Personal account #{ $account }
orbitra-atm-balance =
    { $amount ->
        [one] { $amount } credit
       *[other] { $amount } credits
    }
orbitra-atm-id-slot = ID card
orbitra-atm-insert-card = Insert ID
orbitra-atm-eject-card = Eject ID
orbitra-atm-login = Hold your ID card in your active hand and insert it into the terminal.
orbitra-atm-card-occupied = The card slot is occupied.
orbitra-atm-card-insert-denied = Hold your own ID in your active hand and make sure the slot is empty.
orbitra-atm-card-eject-denied = Couldn't eject your ID card.
orbitra-atm-amount = Amount in credits
orbitra-atm-recipient = Recipient account number
orbitra-atm-withdraw = Withdraw
orbitra-atm-transfer = Transfer
orbitra-atm-deposit = Deposit cash from hand
orbitra-atm-history = Recent transactions
orbitra-atm-entry-start = Starting balance · { $amount }
orbitra-atm-entry-salary = Salary · { $amount }
orbitra-atm-entry-deposit = Cash deposit · { $amount }
orbitra-atm-entry-withdraw = Cash withdrawal · { $amount }
orbitra-atm-entry-transfer = Transfer, account #{ $account } · { $amount }
orbitra-atm-entry-purchase = Purchase · { $amount }
orbitra-atm-entry-unknown = Transaction · { $amount }
orbitra-atm-deposit-denied = Insert your ID and check the cash in your active hand and your balance limit.
orbitra-atm-withdraw-denied = Insert your ID and check your balance.
orbitra-atm-transfer-denied = Insert your ID and check the amount and recipient account number.
orbitra-payroll-notification-title = Payday
orbitra-payroll-notification = Your account was credited with { orbitra-atm-balance }.

ent-OrbitraBankCartridge = banking cartridge
    .desc = A program for viewing your personal account and transferring credits.
orbitra-bank-pda-program-name = Bank
orbitra-bank-pda-loading = Loading account details…
orbitra-bank-pda-insert-id = Insert your ID card into the PDA to access your account.
orbitra-bank-pda-wrong-id = You need your own ID card to access your personal account.
orbitra-bank-pda-copy-account = Copy account number
orbitra-bank-pda-transfer-denied = Transfer failed. Check your ID card, the amount, and the recipient's account number.
orbitra-retail-vending-price = { $price } cr.
orbitra-retail-payment-denied = Payment failed. You need { $price } credits in your account or active hand.
orbitra-bank-pda-load-failed = Could not load the account. Reopen the app to try again.
