- JWT Authentication
- rename and reorganize Utils project
- flesh out rest of customers api
- Error handling/messaging/logging -- basically everywhere!
- Split up roast domain file into several modules
- refactor/split up roast repository functions into several modules

## Incoming SMS Webhook Mappings

### "SUBSCRIBE"

- Corresponds to a Customer.Subscribed event
- Subscribe to text notifications
- Can do this any time

### "UNSUBSCRIBE"

- Corresponds to a Customer.Unsubscribed event
- Unsubscribe from receiving text notifications
- Still will receive texts related to open orders
- Can do this any time

### "CANCEL"

- Cancel an open order on the open roast (if one exists)
- Text should give the customer the option to place a new order if they want
- Can't cancel once a roast is closed; should tell them to reach out to evan directly

### "CONFIRM"

- Confirm an unconfirmed order on the open roast (if one exists)
- Essentially creates an invoice for the order
- Text should give them an option to cancel

### Ordering

- In the format:
2 A
1 B
2 D

- Text will respond with a price/summary and ask them to confirm
- When the roast is closed, a notification text will be sent
