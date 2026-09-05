# -*- coding: utf-8 -*-
"""Vietnamese term lists for the S-2 reader-package validator.

The validator's original scans were written against English prose. Against a
Vietnamese package they would pass **vacuously** - a green run proving only that
the package contains no English. These lists restore the discriminating power,
and they live in their own file so the wording can be reviewed as Vietnamese
rather than as escapes inside a larger script.

Every entry is a phrase, not a bare word: bare Vietnamese words like `đạt` or
`nhãn` occur inside ordinary vocabulary and would fire on correct prose, and a
check that cries wolf gets switched off.
"""

# Terms that would tell a reader something about the measurement, the selection,
# or a row's history. Scanned over the blind files only - the guideline copy and
# its rendering carry frozen v1's own §10 thresholds by design.
BANNED_VI = [
    u"ngưỡng",              # threshold
    u"đạt yêu cầu",         # passes
    u"không đạt",           # fails
    u"bảng đáp án",         # answer key
    u"đáp án đúng",         # the correct answer
    u"tranh chấp",          # contested
    u"niêm phong",          # sealed
    u"giữ riêng",           # reserved
    u"lô đã chấm",          # scored batch
    u"phân xử",             # adjudication
    u"bất đồng",            # disagreement
    u"dự đoán",             # prediction
    u"nhãn từ lượt trước",  # historical label
    u"nhãn lịch sử",        # historical label
    u"lượt đối chiếu",      # reference pass
    u"người gán nhãn kia",  # the other annotator
    u"danh mục ví dụ",      # example catalogue
    u"trải đều độ khó",     # difficulty-spread
    u"mức đồng thuận",      # agreement rate
]

# D-5: the owner has not authorised how an `unresolved` response scores. In a
# warmer Vietnamese register the pull toward reassuring the reader is much
# stronger than it was in English, and this exact class of sentence has already
# had to be removed once. Scanned over the blind files AND the rendering.
D5_CLAIMS_VI = [
    u"được tính là đúng",
    u"tính là đúng",
    u"không bị trừ điểm",
    u"không trừ điểm",
    u"không tính là sai",
    u"hoàn toàn hợp lệ",
    u"vẫn được chấp nhận",
    u"được chấp nhận là",
    u"sẽ được tính",
    u"không ảnh hưởng đến kết quả",
    u"là kết quả đúng",
    u"là câu trả lời đúng",
]

# Strings that rules B-2 and B-4 decide on lexically. In frozen v1 they appear
# only as marked foreign quotations; in Vietnamese prose they are ordinary words.
# The rendering may use them ONLY inside code spans, so that translating the
# guideline cannot move a boundary the guideline decides on.
TRIGGERS = [
    u"giữa kỳ",
    u"cuối kỳ",
    u"kiểm tra",
    u"đồ án",
    u"bài tập lớn",
    u"nhóm",
]
