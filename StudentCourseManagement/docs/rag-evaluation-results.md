# RAG Retrieval Evaluation Results

Generated: 2026-09-29 21:31 by `StudentCourseManagement.Evaluation`.

## Methodology

- **Document**: `Technology_Courses_Guide_10_Pages.pdf` (10 pages, 20 chunks, indexed in SQL Server via the admin upload endpoint)
- **Embeddings**: real `nomic-embed-text` embeddings via Ollama (not the offline test fake)
- **Vector-only mode**: cosine similarity ranking, minimum score 0.25, top 5
- **Hybrid mode**: vector + keyword rankings fused with reciprocal rank fusion (K=60), second-pass quality filter (keyword coverage >= 1/3 of query keywords, or cosine >= 0.50), blended rerank (0.6 x vector + 0.4 x keyword coverage), max 2 chunks per document page, top 5
- **Hit@5**: the page containing the known answer appears anywhere in the top 5 retrieved chunks
- **Question styles**: a mix of keyword-heavy questions and paraphrased questions, to expose where keyword fusion helps

## Results

| # | Question | Expected page | Vector-only top-5 pages | V-hit | Hybrid top-5 pages | H-hit |
|---|----------|--------------|------------------------|-------|--------------------|-------|
| 1 | How do I start learning programming as a complete beginner? | 1 | 1,10,2,3 | yes | 1,3,8,10 | yes |
| 2 | What programming habits help with debugging code? | 1 | 1,6,2 | yes | 1,6,2,3 | yes |
| 3 | What are the four core ideas of object-oriented programming? | 2 | 2,1,6 | yes | 2,1,6,3 | yes |
| 4 | How does encapsulation protect object state? | 2 | 2,1,6,9 | yes | 2,6,1 | yes |
| 5 | When is a hash table better than scanning a list? | 3 | 3,4,2 | yes | 3,4,5 | yes |
| 6 | What graph traversal algorithms are taught in Data Structures? | 3 | 3,10,4,7 | yes | 3,10,4,2,1 | yes |
| 7 | What does Big-O notation describe? | 4 | 4,2,1 | yes | 4,2 | yes |
| 8 | Why can binary search halve the search space? | 4 | 4,3,5 | yes | 4,3 | yes |
| 9 | How do transactions keep a database consistent? | 5 | 5,4,3,2 | yes | 5,2,4,3 | yes |
| 10 | What is normalization in SQL good for? | 5 | 5,4,3 | yes | 5,2,4 | yes |
| 11 | What are deadlocks and race conditions in operating systems? | 6 | 6,1,5,7 | yes | 6,1,5 | yes |
| 12 | How does virtual memory work in an operating system? | 6 | 6,4,2,5 | yes | 6,4,5,8 | yes |
| 13 | What is the difference between TCP and UDP? | 7 | 7,6,4,2 | yes | 7 | yes |
| 14 | What does DNS do on a network? | 7 | 7,6,3,2 | yes | 7,6,3 | yes |
| 15 | What are the differences between IaaS, PaaS and SaaS? | 8 | 8,9,2 | yes | 8,9,2 | yes |
| 16 | How does the cloud help a small application scale with demand? | 8 | 8,7,6,3 | yes | 8,6,4,7 | yes |
| 17 | What is retrieval-augmented generation used for in AI? | 9 | 9,10,8,4 | yes | 9,10,4,3 | yes |
| 18 | What is overfitting in machine learning? | 10 | 10,4,9 | yes | 10,9,8 | yes |
| 19 | How much does parking cost for international students? | none | 5,8,9,7 | - | - | - |

## Summary

- **Vector-only hit@5**: 18/18 (100%)
- **Hybrid hit@5**: 18/18 (100%)

## Refusal check

"How much does parking cost for international students?" - vector-only: 5 results, hybrid: 0 results.
Hybrid retrieval (the production path) returns no chunks above the cutoff, so the retrieved-documents block is empty and the assistant replies "not found in the documents". **PASS**.
Notably, plain vector search alone leaked unrelated chunks for this question: nomic-embed-text cosine similarities cluster tightly, so unrelated text can still clear a 0.25 cutoff. The second-pass keyword-coverage filter rejected those chunks - a concrete precision win for hybrid search.

## Conclusion

Hybrid and vector-only retrieval scored identically on hit@5 (18/18): on this topically well-separated corpus, semantic embeddings alone already rank the right page in the top 5. Hybrid search is still the production default because of the refusal check - plain vector search leaked unrelated chunks for the unanswerable question, while the hybrid second-pass filter correctly returned nothing.
