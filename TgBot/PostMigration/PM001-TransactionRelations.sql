
ALTER TABLE [dbo].[CashLedgerEntry]  WITH CHECK ADD  CONSTRAINT [FK_CashLedgerEntry_CashAccount] FOREIGN KEY([AccountId])
REFERENCES [dbo].[CashAccount] ([Id])
GO

ALTER TABLE [dbo].[CashLedgerEntry] CHECK CONSTRAINT [FK_CashLedgerEntry_CashAccount]
GO

ALTER TABLE [dbo].[CashLedgerEntry]  WITH CHECK ADD  CONSTRAINT [FK_CashLedgerEntry_Transaction] FOREIGN KEY([TransactionId])
REFERENCES [dbo].[Transaction] ([Id])
GO

ALTER TABLE [dbo].[CashLedgerEntry] CHECK CONSTRAINT [FK_CashLedgerEntry_Transaction]
GO

ALTER TABLE [dbo].[CashEntity]  WITH CHECK ADD  CONSTRAINT [FK_CashEntity_Transaction] FOREIGN KEY([TransactionHead])
REFERENCES [dbo].[Transaction] ([Id])
GO

ALTER TABLE [dbo].[CashEntity] CHECK CONSTRAINT [FK_CashEntity_Transaction]
GO

ALTER TABLE [dbo].[Transaction]  WITH CHECK ADD  CONSTRAINT [FK_Transaction_Transaction] FOREIGN KEY([PrevTransaction])
REFERENCES [dbo].[Transaction] ([Id])
GO

ALTER TABLE [dbo].[Transaction] CHECK CONSTRAINT [FK_Transaction_Transaction]
GO

