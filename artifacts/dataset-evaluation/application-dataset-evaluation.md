# Application Dataset Evaluation

- Generated (UTC): 2026-05-12 14:49:22
- Dataset root: `D:\DocumentApp\DocumentFlowAppDataset\dataset\Application`

## train

- Files: 11
- Successful extractions: 11
- Fallback extractions: 0
- Matched fields: 41/44
- Field recall: 93,2 %

### application_001.docx

- Provider: `docx-xml`
- Extraction succeeded: yes
- Fallback: no
- Matched fields: 4/4
- Text preview: `Руководителю от сотрудника: Иванов Иван Иванович Отдел: Отдел делопроизводства ЗАЯВЛЕНИЕ О предоставлении отпуска Прошу предоставить мне ежегодный основной оплачиваемый отпуск с 15 июня 2026 года на 14 календарных дней. ...`

### application_002.docx

- Provider: `docx-xml`
- Extraction succeeded: yes
- Fallback: no
- Matched fields: 4/4
- Text preview: `Руководителю от сотрудника: Смирнова Анна Викторовна Отдел: Архивариат ЗАЯВЛЕНИЕ О замене неисправного оборудования Довожу до вашего сведения, что мой рабочий принтер марки HP LaserJet Pro перестал захватывать бумагу и в...`

### application_003.docx

- Provider: `docx-xml`
- Extraction succeeded: yes
- Fallback: no
- Matched fields: 3/4
- Mismatches:
  - `application_text` expected `Прошу предоставить мне отпуск без сохранения заработной платы 20 мая 2026 года по семейным обстоятельствам.` actual `В связи с необходимостью прохождения курсов повышения квалификации, прошу установить мне индивидуальный график работы с 01 июня 2026 г. по 30 июня 2026 г.: начало рабочего дня в 10:00, окончание в 19:00, перерыв на обед с 14:00 до 15:00`
- Text preview: `Руководителю от сотрудника: Петров А. С. Отдел: ИТ-отдел ЗАЯВЛЕНИЕ Отгул за свой счет В связи с необходимостью прохождения курсов повышения квалификации, прошу установить мне индивидуальный график работы с 01 июня 2026 г...`

### application_004.pdf

- Provider: `pdf-text`
- Extraction succeeded: yes
- Fallback: no
- Matched fields: 3/4
- Mismatches:
  - `employee_name` expected `Салтыкова-Щедрина Екатерина-Виктория Александровна` actual `Салтыкова Екатерина Александровна`
- Text preview: `Руководителю от сотрудника: Салтыкова Екатерина Александровна Отдел: Сектор по работе с юридическими лицами и наследственными делами ЗАЯВЛЕНИЕ О выдаче доверенности Прошу выдать мне доверенность на право представления ин...`

### application_005.pdf

- Provider: `pdf-text`
- Extraction succeeded: yes
- Fallback: no
- Matched fields: 4/4
- Text preview: `Руководителю от сотрудника: Константинов Дмитрий Олегович Отдел: Отдел переводов ЗАЯВЛЕНИЕ Об изменении режима рабочего времени В связи с необходимостью прохождения курсов повышения квалификации, прошу установить мне инд...`

### application_006.pdf

- Provider: `pdf-text`
- Extraction succeeded: yes
- Fallback: no
- Matched fields: 4/4
- Text preview: `Руководителю от сотрудника: Сидоров В.В. Отдел: Служба помощников нотариуса ЗАЯВЛЕНИЕ Запрос на доступ к архиву В рамках подготовки ответа на запрос Следственного комитета РФ № 45/12-26 от 10.05.2026, прошу предоставить ...`

### application_007.docx

- Provider: `docx-xml`
- Extraction succeeded: yes
- Fallback: no
- Matched fields: 4/4
- Text preview: `КОМУ: Руководству нотариальной конторы ОТДЕЛ: Отдел кадров ОТ КОГО: Ковалев Игорь Владимирович Тема: О прохождении диспансеризации Настоящим довожу до вашего сведения следующую информациюо моем отсутствии на рабочем мест...`

### application_008.docx

- Provider: `docx-xml`
- Extraction succeeded: yes
- Fallback: no
- Matched fields: 4/4
- Text preview: `КОМУ: Руководству нотариальной конторы ОТДЕЛ: ИТ ОТ КОГО: Юн А. И. Тема: Утеря пропуска Настоящим довожу до вашего сведения следующую информацию об утере моего электронного пропуска для доступа в здание. Прошу рассмотрет...`

### application_010.pdf

- Provider: `pdf-text`
- Extraction succeeded: yes
- Fallback: no
- Matched fields: 3/4
- Mismatches:
  - `employee_name` expected `Мамин-Сибиряк Константин-Александр Дмитриевич` actual `Сибиряк Александр Дмитриевич`
- Text preview: `КОМУ: Руководству нотариальной конторы ОТДЕЛ: Сектор по взаимодействию с государственными и муниципальными органами ОТ КОГО: Сибиряк Александр Дмитриевич Тема: О направлении срочного запроса в Росреестр Настоящим довожу ...`

### application_013.pdf

- Provider: `manual-transcript`
- Extraction succeeded: yes
- Fallback: no
- Matched fields: 4/4
- Text preview: `Руководителю от сотрудника: Белоусова Мария Александровна Отдел: Отдел оформления наследственных прав ЗАЯВЛЕНИЕ Тема: О выделении дополнительных канцелярских и расходных материалов В связи с резким увеличением количества...`

### application_015.png

- Provider: `manual-transcript`
- Extraction succeeded: yes
- Fallback: no
- Matched fields: 4/4
- Text preview: `Руководителю от сотрудника: Оглы Рустам Вагифович Отдел: АХО (Административно-хозяйственный отдел) ЗАЯВЛЕНИЕ Тема: Заявка на проведение ремонтных работ Прошу организовать срочный вызов мастера для ремонта системы кондици...`


## test

- Files: 4
- Successful extractions: 4
- Fallback extractions: 0
- Matched fields: 16/16
- Field recall: 100,0 %

### application_009.docx

- Provider: `docx-xml`
- Extraction succeeded: yes
- Fallback: no
- Matched fields: 4/4
- Text preview: `КОМУ: Руководству нотариальной конторы ОТДЕЛ: Отдел информационного обеспечения ОТ КОГО: Мельникова Светлана Юрьевна Тема: О сбое в работе базы данных Настоящим довожу до вашего сведения следующую информацию о критическо...`

### application_011.pdf

- Provider: `pdf-text`
- Extraction succeeded: yes
- Fallback: no
- Matched fields: 4/4
- Text preview: `КОМУ: Руководству нотариальной конторы ОТДЕЛ: Бухгалтерия ОТ КОГО: Николаев Н. Н. Тема: Ошибочное списание средств Настоящим довожу до вашего сведения следующую информациюо выявленном факте двойного списания комиссии бан...`

### application_012.pdf

- Provider: `pdf-text`
- Extraction succeeded: yes
- Fallback: no
- Matched fields: 4/4
- Text preview: `КОМУ: Руководству нотариальной конторы ОТДЕЛ: Служба помощников нотариуса ОТ КОГО: Григорьева В. А. Тема: Выявленный факт подделки документов Настоящим довожу до вашего сведения следующую информацию о том, что при визуал...`

### application_014.pdf

- Provider: `manual-transcript`
- Extraction succeeded: yes
- Fallback: no
- Matched fields: 4/4
- Text preview: `Руководителю от сотрудника: к.ю.н. Воронцов А. Д. Отдел: Юридический отдел по вопросам корпоративного права ЗАЯВЛЕНИЕ Тема: Обнаружение технической ошибки в проекте договора Сообщаю о выявлении существенной технической о...`

