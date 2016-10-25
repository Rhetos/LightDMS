Guid userId = _domRepository.Common.Principal.Query()
						.Where(p => p.Name == _executionContext.UserInfo.UserName)
						.Select(p => p.ID).Single();

foreach (var item in insertedNew)
	item.CreatedByID = userId;

