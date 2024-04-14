# Word2Vec Next Generation: Lightweight single-pass probabilistic word embeddings for TinyGPT

## Similar meaning words detection
Word embedding schemes play a crucial role in natural language processing tasks like detecting similar meaning words and enhancing language models such as ChatGPT. Here's how these schemes contribute to achieving these objectives:

### Semantic Similarity:

Word embeddings map words on a high-dimensional vector space where similar words are located closer to each other. This enables algorithms to measure semantic similarity between words based on the proximity of their embedding vectors.
For example, in a word embedding space, vectors representing words like "laptop" and "computer" would be closer together compared to words like "laptop" and "banana," indicating their semantic similarity.

### Contextual Understanding:

Word embeddings capture the contextual information of words based on their co-occurrence patterns within a corpus of text. Words that appear in similar contexts tend to have similar embedding representations.
Language models like ChatGPT utilize word embeddings to understand the context of input words or phrases. By analyzing the embeddings of neighboring words, the model can infer the meaning of the input and generate appropriate responses.

### Generalization:

Word embedding schemes generalize relationships between words beyond their surface forms. This allows language models to recognize synonyms, antonyms, analogies, and other semantic relationships between words even if they haven't explicitly encountered them during training.
For instance, if a language model has been trained on word embeddings that capture the relationship between "king" and "queen," it can generalize this relationship to predict that "man" is to "woman" as "king" is to "queen" without explicitly being trained on this analogy.

### Enhancing Language Models:

Incorporating word embeddings into language models like ChatGPT enriches their understanding of language semantics and improves their ability to generate coherent and contextually relevant responses.
By leveraging the semantic information encoded in word embeddings, ChatGPT can return similar results for input words with similar meanings. For example, if provided with input words like "happy" and "joyful," ChatGPT can generate responses that convey similar sentiments or emotions.
In summary, word embedding schemes enable language models like ChatGPT to understand the semantic relationships between words, generalize across language patterns, and generate contextually appropriate responses based on the input they receive. By leveraging the rich semantic information encoded in word embeddings, these models can better comprehend and produce human-like language interactions.

## An example of an embedding scheme (NOT Word2Vec-NG) putting similar topic books near each other
![image](https://github.com/jessiepathfinder/Word2VecNG/assets/55774978/e069ca36-9606-4960-87aa-73cac747bd5a)

## How it works

The rationale for how Word2Vec-NG can detect similar meaning words like "laptop" and "computer" based on hashed collocation counts lies in the principle that words with similar contexts tend to have similar meanings. By analyzing the surrounding words of a target word in a large corpus of text, Word2Vec-NG can capture the semantic relationships between words and represent them in a high-dimensional vector space.

Here's how Word2Vec-NG achieves this using hashed collocation counts:

**Context Window**: Word2Vec-NG considers the words that appear within a certain context window around the target word. This context window defines the neighboring words that contribute to the representation of the target word.

**Hashed Collocation Counts**: For each target word encountered in the corpus, Word2Vec-NG calculates hashed collocation counts for its surrounding words. These counts capture the co-occurrence patterns of words within the context window.

**Similarity Measure**: Words with similar hashed collocation counts are likely to have similar meanings. For example, if "laptop" and "computer" frequently appear in similar contexts (e.g., "laptop" often co-occurs with "keyboard," "screen," "battery," etc., similar to how "computer" does), their hashed collocation counts will be similar.

**Vector Representation**: The hashed collocation counts are used to generate vector representations (word embeddings) for each word in the vocabulary. Similar words will have vectors that are closer together in the high-dimensional vector space, indicating their semantic similarity.

**Example**: Suppose "laptop" and "computer" frequently appear in similar contexts, such as "I use my laptop to browse the internet" and "I use my computer to browse the internet." In these sentences, "laptop" and "computer" share similar surrounding words ("use," "browse," "internet"), leading to similar hashed collocation counts and, consequently, similar vector representations in Word2Vec-NG.

By leveraging these principles, Word2Vec-NG can effectively detect and represent the semantic similarities between words like "laptop" and "computer" based on their co-occurrence patterns in text corpora.

## Improvements over the original Word2Vec algorithm

Word2Vec-NG, an evolution of the original Word2Vec algorithm, introduces several enhancements that improve its effectiveness in capturing semantic relationships between words. Here's how Word2Vec-NG builds upon the original Word2Vec algorithm:

### Enhanced Collocation Counting:

Word2Vec-NG employs a more sophisticated approach to collocation counting compared to the original Word2Vec. By using hashed collocation counts, it captures not only ordered context but also unordered context, past, and future collocations.
This allows Word2Vec-NG to capture a wider range of word relationships, including bidirectional associations, past and future dependencies, and ordered collocations, leading to richer word embeddings.

### Efficient Handling of Rare Words:

Word2Vec-NG incorporates techniques to handle rare words more effectively. By leveraging probabilistic data structures and the hashing trick, it can better represent and learn from infrequent words, which often pose challenges for traditional word embedding models.
This ensures that rare words are adequately represented in the embedding space, leading to more robust and comprehensive word embeddings.

### Single-Pass Approach:

One of the key innovations of Word2Vec-NG is its single-pass approach. Unlike traditional Word2Vec models that require iterative training on large text corpora, Word2Vec-NG computes word embeddings in a single pass through the data.
This single-pass technique significantly reduces computational overhead and simplifies the embedding generation process, making it more efficient and scalable for large datasets.

## Improved Attention Mechanism:

Word2Vec-NG incorporates an enhanced attention mechanism that optimizes attention across its four collocation modes: bidirectional, past, future, and ordered.
By improving attention allocation, Word2Vec-NG ensures that meaningful contextual information is captured effectively, leading to more accurate word embeddings and better representation of semantic relationships.
