"""
Build a complete NER ONNX model from chinese-roberta-wwm-ext.

Adds a token classification head (Linear: 768 → num_labels) on top of
the pre-trained encoder and exports the full pipeline to ONNX.

Usage:
    cd /home/gulu/agent_qc_cc
    pip install torch transformers onnx onnxruntime
    python scripts/ner-model/build_ner_onnx.py

Output:
    knowledge/models/roberta-ner.onnx  — encoder + NER head (FP16)
    knowledge/models/roberta-ner-tokenizer.json — tokenizer config for C#

The NER head is randomly initialized. Fine-tune on CMeEE for accuracy.
See scripts/ner-model/finetune_cmeee.py for the training step.
"""

import os
import sys
import json
from pathlib import Path

import torch
import torch.nn as nn
from transformers import AutoModel, AutoTokenizer

# Paths
REPO_ROOT = Path(__file__).resolve().parent.parent.parent
MODEL_SRC = REPO_ROOT / "chinese-roberta-wwm-ext"
FP32_ONNX = MODEL_SRC / "roberta_wwm_ext_onnx_fp32" / "model.onnx"
OUTPUT_DIR = REPO_ROOT / "knowledge" / "models"

# CMeEE NER label set (9 entity types × BIO + O = 19 labels)
NER_LABELS = [
    "O",
    # 疾病 disease
    "B-dis", "I-dis",
    # 症状 symptom
    "B-sym", "I-sym",
    # 药物 drug
    "B-dru", "I-dru",
    # 医疗程序 procedure
    "B-pro", "I-pro",
    # 医疗设备 equipment
    "B-equ", "I-equ",
    # 检查 imaging/exam
    "B-ite", "I-ite",
    # 身体部位 body/anatomy
    "B-bod", "I-bod",
    # 科室 department
    "B-dep", "I-dep",
    # 微生物 microorganism
    "B-mic", "I-mic",
]

NUM_LABELS = len(NER_LABELS)
ID2LABEL = {i: label for i, label in enumerate(NER_LABELS)}
LABEL2ID = {label: i for i, label in enumerate(NER_LABELS)}


class RobertaForNER(nn.Module):
    """RoBERTa encoder + token classification head for NER."""

    def __init__(self, model_dir: str, num_labels: int):
        super().__init__()
        self.encoder = AutoModel.from_pretrained(model_dir, torch_dtype=torch.float16)
        self.dropout = nn.Dropout(0.1)
        self.classifier = nn.Linear(768, num_labels)

    def forward(self, input_ids, attention_mask, token_type_ids=None):
        outputs = self.encoder(
            input_ids=input_ids,
            attention_mask=attention_mask,
            token_type_ids=token_type_ids,
        )
        sequence_output = outputs.last_hidden_state
        sequence_output = self.dropout(sequence_output)
        logits = self.classifier(sequence_output)
        return logits


def export_onnx(model, tokenizer, output_path: Path):
    """Export the full NER model to ONNX with dynamic batch/sequence axes."""

    model.eval()
    model = model.half()  # FP16

    # Dummy inputs (batch=1, seq_len=32)
    dummy_input_ids = torch.zeros(1, 32, dtype=torch.long)
    dummy_attention_mask = torch.ones(1, 32, dtype=torch.long)
    dummy_token_type_ids = torch.zeros(1, 32, dtype=torch.long)

    output_path.parent.mkdir(parents=True, exist_ok=True)

    torch.onnx.export(
        model,
        (dummy_input_ids, dummy_attention_mask, dummy_token_type_ids),
        str(output_path),
        input_names=["input_ids", "attention_mask", "token_type_ids"],
        output_names=["logits"],
        dynamic_axes={
            "input_ids": {0: "batch_size", 1: "sequence_length"},
            "attention_mask": {0: "batch_size", 1: "sequence_length"},
            "token_type_ids": {0: "batch_size", 1: "sequence_length"},
            "logits": {0: "batch_size", 1: "sequence_length"},
        },
        opset_version=17,
        do_constant_folding=True,
    )
    print(f"ONNX model saved to {output_path} ({output_path.stat().st_size / 1024 / 1024:.1f} MB)")


def save_tokenizer_config(tokenizer, output_dir: Path):
    """Save tokenizer config for C# BERT tokenizer."""
    output_dir.mkdir(parents=True, exist_ok=True)

    config = {
        "vocab_size": tokenizer.vocab_size,
        "max_length": 512,
        "pad_token_id": tokenizer.pad_token_id,
        "cls_token_id": tokenizer.cls_token_id,
        "sep_token_id": tokenizer.sep_token_id,
        "unk_token_id": tokenizer.unk_token_id,
        "mask_token_id": tokenizer.mask_token_id,
        "label_list": NER_LABELS,
        "id2label": ID2LABEL,
        "label2id": LABEL2ID,
    }

    config_path = output_dir / "roberta-ner-tokenizer.json"
    with open(config_path, "w", encoding="utf-8") as f:
        json.dump(config, f, ensure_ascii=False, indent=2)
    print(f"Tokenizer config saved to {config_path}")

    # Also copy vocab.txt for C# tokenizer
    import shutil
    vocab_src = MODEL_SRC / "vocab.txt"
    vocab_dst = output_dir / "vocab.txt"
    if vocab_src.exists():
        shutil.copy(vocab_src, vocab_dst)
        print(f"vocab.txt copied to {vocab_dst}")


def main():
    print("Loading pre-trained chinese-roberta-wwm-ext...")
    tokenizer = AutoTokenizer.from_pretrained(str(MODEL_SRC))
    model = RobertaForNER(str(MODEL_SRC), NUM_LABELS)

    print(f"Model: {sum(p.numel() for p in model.parameters()) / 1e6:.1f}M parameters")
    print(f"NER labels: {NUM_LABELS} ({', '.join(NER_LABELS[:9])}...)")
    print()

    print("Exporting to ONNX...")
    onnx_path = OUTPUT_DIR / "roberta-ner.onnx"
    export_onnx(model, tokenizer, onnx_path)

    print()
    print("Saving tokenizer config...")
    save_tokenizer_config(tokenizer, OUTPUT_DIR)

    print()
    print("Done! Files in", OUTPUT_DIR)
    for f in sorted(OUTPUT_DIR.iterdir()):
        print(f"  {f.name} ({f.stat().st_size / 1024 / 1024:.1f} MB)" if f.suffix == ".onnx" else f"  {f.name}")

    print()
    print("NOTE: The NER head is randomly initialized.")
    print("For production accuracy, fine-tune on CMeEE:")
    print("  python scripts/ner-model/finetune_cmeee.py")


if __name__ == "__main__":
    main()
